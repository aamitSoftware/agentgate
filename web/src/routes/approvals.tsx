import { useNavigate, useSearch } from "@tanstack/react-router";
import { useDecideGrant, useGrantQueue, useReviewQueue, useReviews, undecidedReviews } from "@/api/queries";
import type { ReviewGrant, ReviewSummary } from "@/api/types";
import { DataTable, RowLink, type Column } from "@/components/data-table";
import { DecisionPanel } from "@/components/decision-panel";
import { Hash } from "@/components/hash";
import { PageBody, PageHeader } from "@/components/page-header";
import { Message, QueryState } from "@/components/query-state";
import { Seals } from "@/components/seals";
import { RiskBadge, ReviewStatusBadge } from "@/components/status";
import { Expiry, RelativeTime } from "@/components/time";
import { ToolList } from "@/components/tool-list";
import { Card } from "@/components/ui/card";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { count } from "@/lib/format";
import type { ApprovalTab } from "./search";

const serverCell: Column<ReviewSummary> = {
  id: "server",
  header: "Server",
  sortValue: (review) => review.serverName,
  cell: (review) => (
    <RowLink to="/approvals/reviews/$reviewId" params={{ reviewId: review.id }} className="font-mono" translate="no">
      {review.serverName}
    </RowLink>
  ),
};

const riskOrder = { High: 0, Medium: 1, Low: 2 } as const;

const queueColumns: Column<ReviewSummary>[] = [
  serverCell,
  { id: "risk", header: "Risk", sortValue: (review) => riskOrder[review.riskLevel], cell: (review) => <RiskBadge risk={review.riskLevel} /> },
  { id: "findings", header: "Findings", numeric: true, sortValue: (review) => review.findingCount, cell: (review) => count(review.findingCount) },
  {
    id: "kind",
    header: "Kind",
    cell: (review) => (review.previousHash ? <span className="text-danger">Re-review after drift</span> : "New server"),
  },
  { id: "manifest", header: "Manifest", interactive: true, cell: (review) => <Hash value={review.manifestHash} /> },
  { id: "approvals", header: "Approvals", cell: (review) => <Seals firstApproverId={review.firstApproverId} complete={false} /> },
  {
    id: "waiting",
    header: "Waiting since",
    sortValue: (review) => review.createdOn,
    cell: (review) => <RelativeTime value={review.createdOn} className="text-muted-foreground" />,
  },
];

const historyColumns: Column<ReviewSummary>[] = [
  serverCell,
  { id: "decision", header: "Decision", sortValue: (review) => review.status, cell: (review) => <ReviewStatusBadge status={review.status} /> },
  { id: "risk", header: "Risk", sortValue: (review) => riskOrder[review.riskLevel], cell: (review) => <RiskBadge risk={review.riskLevel} /> },
  { id: "manifest", header: "Manifest", interactive: true, cell: (review) => <Hash value={review.manifestHash} /> },
  {
    id: "opened",
    header: "Opened",
    sortValue: (review) => review.createdOn,
    cell: (review) => <RelativeTime value={review.createdOn} className="text-muted-foreground" />,
  },
];

export function ApprovalsPage() {
  const { tab = "reviews" } = useSearch({ from: "/approvals" });
  const navigate = useNavigate({ from: "/approvals" });
  const reviews = useReviewQueue();
  const grants = useGrantQueue();
  const history = useReviews();

  return (
    <>
      <PageHeader
        title="Approvals"
        meta="Nothing reaches an agent until two different reviewers approve it: manifests before they are pinned, grants before they are enforced."
      />
      <PageBody>
        <Tabs value={tab} onValueChange={(next) => void navigate({ search: { tab: next === "reviews" ? undefined : (next as ApprovalTab) }, replace: true })}>
          <TabsList>
            <TabsTrigger value="reviews">
              Manifest reviews {reviews.data && <span className="text-xs text-muted-foreground tabular">{reviews.data.length}</span>}
            </TabsTrigger>
            <TabsTrigger value="grants">
              Grant requests {grants.data && <span className="text-xs text-muted-foreground tabular">{grants.data.length}</span>}
            </TabsTrigger>
            <TabsTrigger value="decided">Decided</TabsTrigger>
          </TabsList>

          <TabsContent value="reviews" className="mt-4">
            <Card className="overflow-hidden">
              <QueryState
                query={reviews}
                empty={<Message title="No manifest waiting">New servers and drifted manifests appear here for two reviewers to decide.</Message>}
              >
                {(data) => <DataTable label="Manifest reviews waiting" columns={queueColumns} rows={data} rowKey={(review) => review.id} />}
              </QueryState>
            </Card>
          </TabsContent>

          <TabsContent value="grants" className="mt-4 flex flex-col gap-4">
            <QueryState
              query={grants}
              empty={
                <Card>
                  <Message title="No grant request waiting">Admins request grants for agents from the Grants page.</Message>
                </Card>
              }
            >
              {(data) => data.map((grant) => <GrantRequest key={grant.id} grant={grant} />)}
            </QueryState>
          </TabsContent>

          <TabsContent value="decided" className="mt-4">
            <Card className="overflow-hidden">
              <QueryState
                query={history}
                isEmpty={(data) => data.every((review) => undecidedReviews.includes(review.status))}
                empty={<Message title="No decisions yet">Approved, rejected and superseded reviews are kept here with their audit trail.</Message>}
              >
                {(data) => (
                  <DataTable
                    label="Decided manifest reviews"
                    columns={historyColumns}
                    rows={data.filter((review) => !undecidedReviews.includes(review.status))}
                    rowKey={(review) => review.id}
                  />
                )}
              </QueryState>
            </Card>
          </TabsContent>
        </Tabs>
      </PageBody>
    </>
  );
}

function GrantRequest({ grant }: { grant: ReviewGrant }) {
  const decide = useDecideGrant();
  return (
    <Card className="grid gap-6 p-4 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="flex min-w-0 flex-col gap-3">
        <h3 className="flex flex-wrap items-center gap-2 text-base font-semibold">
          <span className="font-mono">{grant.agentClientId}</span>
          <span className="font-normal text-muted-foreground">may call, on</span>
          <span className="font-mono">{grant.serverName}</span>
        </h3>
        <ToolList tools={grant.tools} />
        <blockquote className="border-l-2 pl-3 text-muted-foreground">{grant.justification}</blockquote>
        <dl className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">Expires</dt>
            <dd>
              <Expiry value={grant.expiresAt} />
            </dd>
          </div>
          <div className="flex gap-1.5">
            <dt className="text-muted-foreground">Requested</dt>
            <dd>
              <RelativeTime value={grant.createdOn} />
            </dd>
          </div>
        </dl>
      </div>
      <DecisionPanel
        subject="the grant"
        firstApproverId={grant.firstApproverId}
        complete={grant.status === "Active"}
        undecided={grant.status === "Requested" || grant.status === "PendingApproval"}
        closedText={`This request is ${grant.status.toLowerCase()}.`}
        finalApproveLabel="Approve and activate"
        finalNote="Activation is checked live against Registry: every tool must be in the server's pinned manifest."
        rejectConsequence={`${grant.agentClientId} will not get these tools on ${grant.serverName}; a new request starts over.`}
        onDecide={(verb, comment) => decide.mutateAsync({ grantId: grant.id, verb, comment })}
      />
    </Card>
  );
}
