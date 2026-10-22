import { queryOptions, useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { api, post } from "./client";
import type {
  ActivityItem,
  AgentDetail,
  AgentSummary,
  GrantStatus,
  HubGrant,
  ReviewDetail,
  ReviewGrant,
  ReviewStatus,
  ReviewSummary,
  ServerDetail,
  ServerSummary,
} from "./types";

// The Hub is eventually consistent (it learns from events), so its views refresh on an interval while the
// tab is visible; Review is the source of truth for decisions and is refetched after every action.
const listRefresh = 5_000;
const detailRefresh = 10_000;

export const undecidedReviews: ReviewStatus[] = ["Open", "PartiallyApproved"];
export const undecidedGrants: GrantStatus[] = ["Requested", "PendingApproval"];

const oldestFirst = <T extends { createdOn: string }>(items: T[]) => items.sort((left, right) => left.createdOn.localeCompare(right.createdOn));

export const queries = {
  servers: () => queryOptions({ queryKey: ["hub", "servers"], queryFn: () => api<ServerSummary[]>("hub", "/servers"), refetchInterval: listRefresh }),
  server: (serverId: string) =>
    queryOptions({ queryKey: ["hub", "servers", serverId], queryFn: () => api<ServerDetail>("hub", `/servers/${serverId}`), refetchInterval: detailRefresh }),
  agents: () => queryOptions({ queryKey: ["hub", "agents"], queryFn: () => api<AgentSummary[]>("hub", "/agents"), refetchInterval: listRefresh }),
  agent: (agentId: string) =>
    queryOptions({ queryKey: ["hub", "agents", agentId], queryFn: () => api<AgentDetail>("hub", `/agents/${agentId}`), refetchInterval: detailRefresh }),
  access: () => queryOptions({ queryKey: ["hub", "access"], queryFn: () => api<HubGrant[]>("hub", "/access"), refetchInterval: listRefresh }),
  /** The snapshot the live feed starts from; refetched on every visit so the gap while away is filled. */
  recentActivity: () =>
    queryOptions({ queryKey: ["hub", "activity"], queryFn: () => api<ActivityItem[]>("hub", "/activity?limit=200"), staleTime: 0, refetchOnMount: "always" }),

  /** The approvals queue: everything waiting for a reviewer, oldest first (first in, first decided). */
  reviewQueue: () =>
    queryOptions({
      queryKey: ["review", "reviews", "undecided"],
      queryFn: async () =>
        oldestFirst((await Promise.all(undecidedReviews.map((status) => api<ReviewSummary[]>("review", `/reviews?status=${status}`)))).flat()),
      refetchInterval: listRefresh,
    }),
  /** Every review, newest first: the decided ones are the approvals history. */
  reviews: () =>
    queryOptions({
      queryKey: ["review", "reviews", "all"],
      queryFn: async () => (await api<ReviewSummary[]>("review", "/reviews")).sort((left, right) => right.createdOn.localeCompare(left.createdOn)),
    }),
  review: (reviewId: string) => queryOptions({ queryKey: ["review", "reviews", reviewId], queryFn: () => api<ReviewDetail>("review", `/reviews/${reviewId}`) }),
  grantQueue: () =>
    queryOptions({
      queryKey: ["review", "grants", "undecided"],
      queryFn: async () =>
        oldestFirst((await Promise.all(undecidedGrants.map((status) => api<ReviewGrant[]>("review", `/grants?status=${status}`)))).flat()),
      refetchInterval: listRefresh,
    }),
  /** Every grant in every state, newest first. */
  grants: () =>
    queryOptions({
      queryKey: ["review", "grants", "all"],
      queryFn: async () => (await api<ReviewGrant[]>("review", "/grants")).sort((left, right) => right.createdOn.localeCompare(left.createdOn)),
      refetchInterval: listRefresh,
    }),
};

export const useServers = () => useQuery(queries.servers());
export const useServer = (serverId: string) => useQuery({ ...queries.server(serverId), enabled: !!serverId });
export const useAgents = () => useQuery(queries.agents());
export const useAgent = (agentId: string) => useQuery({ ...queries.agent(agentId), enabled: !!agentId });
export const useAccess = () => useQuery(queries.access());
export const useRecentActivity = () => useQuery(queries.recentActivity());
export const useReviewQueue = () => useQuery(queries.reviewQueue());
export const useReviews = () => useQuery(queries.reviews());
export const useReview = (reviewId: string) => useQuery({ ...queries.review(reviewId), enabled: !!reviewId });
export const useGrantQueue = () => useQuery(queries.grantQueue());
export const useGrants = () => useQuery(queries.grants());

export const useDecideReview = (reviewId: string) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ verb, comment }: { verb: "approve" | "reject"; comment: string }) =>
      post("review", `/reviews/${reviewId}:${verb}`, { comment: comment || null }),
    // Review changed now; the Hub follows by event and its polling picks that up.
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["review", "reviews"] }),
  });
};

export type GrantVerb = "approve" | "reject" | "suspend" | "resume" | "revoke";

const optimisticStatus: Partial<Record<GrantVerb, GrantStatus>> = { suspend: "Suspended", resume: "Active", revoke: "Revoked" };

type GrantLike = { id: string; status: string };

/** Sets one grant's status in every cached list or detail that shows it; returns how to undo it. */
function patchGrantStatus(queryClient: QueryClient, grantId: string, status: GrantStatus): () => void {
  const patchList = <T extends GrantLike>(grants: T[]) => grants.map((grant) => (grant.id === grantId ? { ...grant, status } : grant));
  const snapshots = [
    ...queryClient.getQueriesData<GrantLike[]>({ queryKey: ["review", "grants"] }),
    ...queryClient.getQueriesData<{ grants: GrantLike[] }>({ queryKey: ["hub", "servers"] }),
    ...queryClient.getQueriesData<{ grants: GrantLike[] }>({ queryKey: ["hub", "agents"] }),
  ];
  for (const [key, data] of snapshots) {
    if (Array.isArray(data)) queryClient.setQueryData(key, patchList(data));
    else if (data && "grants" in data) queryClient.setQueryData(key, { ...data, grants: patchList(data.grants) });
  }
  return () => snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data));
}

export const useDecideGrant = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ grantId, verb, comment }: { grantId: string; verb: GrantVerb; comment: string }) =>
      post("review", `/grants/${grantId}:${verb}`, { comment: comment || null }),
    onMutate: async ({ grantId, verb }) => {
      const status = optimisticStatus[verb];
      if (!status) return undefined;
      await queryClient.cancelQueries({ queryKey: ["review", "grants"] });
      return { undo: patchGrantStatus(queryClient, grantId, status) };
    },
    onError: (_error, _variables, context) => context?.undo(),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["review", "grants"] }),
  });
};

export interface GrantRequest {
  agentId: string;
  serverId: string;
  tools: string[];
  expiresAt: string | null;
  justification: string;
}

export const useRequestGrant = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: GrantRequest) => post<{ id: string }>("review", "/grants", request),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["review", "grants"] }),
  });
};
