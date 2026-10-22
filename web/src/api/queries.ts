import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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

// The Hub is eventually consistent (it learns from events), so its views refresh on an interval;
// Review is the source of truth for decisions and is refetched after every action.
const hubRefresh = 5_000;

export const useServers = () =>
  useQuery({ queryKey: ["hub", "servers"], queryFn: () => api<ServerSummary[]>("hub", "/servers"), refetchInterval: hubRefresh });

export const useServer = (serverId: string) =>
  useQuery({
    queryKey: ["hub", "servers", serverId],
    queryFn: () => api<ServerDetail>("hub", `/servers/${serverId}`),
    refetchInterval: hubRefresh,
    enabled: !!serverId,
  });

export const useAgents = () =>
  useQuery({ queryKey: ["hub", "agents"], queryFn: () => api<AgentSummary[]>("hub", "/agents"), refetchInterval: hubRefresh });

export const useAgent = (agentId: string) =>
  useQuery({ queryKey: ["hub", "agents", agentId], queryFn: () => api<AgentDetail>("hub", `/agents/${agentId}`), refetchInterval: hubRefresh });

export const useAccess = () =>
  useQuery({ queryKey: ["hub", "access"], queryFn: () => api<HubGrant[]>("hub", "/access"), refetchInterval: hubRefresh });

export const useRecentActivity = () =>
  useQuery({ queryKey: ["hub", "activity"], queryFn: () => api<ActivityItem[]>("hub", "/activity?limit=200"), staleTime: Infinity });

const undecidedReviews: ReviewStatus[] = ["Open", "PartiallyApproved"];
const undecidedGrants: GrantStatus[] = ["Requested", "PendingApproval"];

/** The approvals queue: everything waiting for a reviewer, oldest first (first in, first decided). */
export const useReviewQueue = () =>
  useQuery({
    queryKey: ["review", "reviews", "undecided"],
    queryFn: async () =>
      (await Promise.all(undecidedReviews.map((status) => api<ReviewSummary[]>("review", `/reviews?status=${status}`))))
        .flat()
        .sort((left, right) => left.createdOn.localeCompare(right.createdOn)),
    refetchInterval: hubRefresh,
  });

export const useGrantQueue = () =>
  useQuery({
    queryKey: ["review", "grants", "undecided"],
    queryFn: async () =>
      (await Promise.all(undecidedGrants.map((status) => api<ReviewGrant[]>("review", `/grants?status=${status}`))))
        .flat()
        .sort((left, right) => left.createdOn.localeCompare(right.createdOn)),
    refetchInterval: hubRefresh,
  });

export const useReview = (reviewId: string) =>
  useQuery({ queryKey: ["review", "reviews", reviewId], queryFn: () => api<ReviewDetail>("review", `/reviews/${reviewId}`) });

export const useServerReviews = (serverId: string) =>
  useQuery({
    queryKey: ["review", "reviews", "server", serverId],
    queryFn: () => api<ReviewSummary[]>("review", `/reviews?serverId=${serverId}`),
  });

function useReviewMutation<TVariables>(action: (variables: TVariables) => Promise<void>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: action,
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: ["review"] });
      void queryClient.invalidateQueries({ queryKey: ["hub"] });
    },
  });
}

export const useDecideReview = (reviewId: string) =>
  useReviewMutation(({ verb, comment }: { verb: "approve" | "reject"; comment: string }) =>
    post("review", `/reviews/${reviewId}:${verb}`, { comment: comment || null }),
  );

export type GrantVerb = "approve" | "reject" | "suspend" | "resume" | "revoke";

export const useDecideGrant = () =>
  useReviewMutation(({ grantId, verb, comment }: { grantId: string; verb: GrantVerb; comment: string }) =>
    post("review", `/grants/${grantId}:${verb}`, { comment: comment || null }),
  );

export interface GrantRequest {
  agentId: string;
  serverId: string;
  tools: string[];
  expiresAt: string | null;
  justification: string;
}

export const useRequestGrant = () =>
  useReviewMutation(async (request: GrantRequest) => {
    await post<{ id: string }>("review", "/grants", request);
  });
