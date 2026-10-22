import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, createRoute, createRouter, Link, redirect } from "@tanstack/react-router";
import { Compass } from "lucide-react";
import { queries } from "@/api/queries";
import { PageBody, PageHeader } from "@/components/page-header";
import { ErrorState, Message } from "@/components/query-state";
import { Shell } from "@/components/shell";
import { Button } from "@/components/ui/button";
import { ActivityPage } from "@/routes/activity";
import { AgentPage } from "@/routes/agent";
import { AgentsPage } from "@/routes/agents";
import { ApprovalsPage } from "@/routes/approvals";
import { GrantsPage } from "@/routes/grants";
import { OverviewPage } from "@/routes/overview";
import { ReviewPage } from "@/routes/review";
import { activitySearch, agentsSearch, approvalsSearch, grantsSearch, serversSearch } from "@/routes/search";
import { ServerPage } from "@/routes/server";
import { ServersPage } from "@/routes/servers";

interface RouterContext {
  queryClient: QueryClient;
}

const root = createRootRouteWithContext<RouterContext>()({
  component: Shell,
  notFoundComponent: () => (
    <>
      <PageHeader title="Page not found" />
      <PageBody>
        <Message
          icon={<Compass />}
          title="Nothing here"
          action={
            <Button variant="outline" asChild>
              <Link to="/">Go to the overview</Link>
            </Button>
          }
        >
          The link may be old, or the page has moved.
        </Message>
      </PageBody>
    </>
  ),
  errorComponent: ({ error, reset }) => (
    <>
      <PageHeader title="Something went wrong" />
      <PageBody>
        <ErrorState error={error} retry={reset} />
      </PageBody>
    </>
  ),
});

// Loaders only prefetch (they never block or throw): hovering a link warms the cache, and the page renders
// its own loading and error states from the query.
const routes = [
  createRoute({ getParentRoute: () => root, path: "/", component: OverviewPage }),
  createRoute({
    getParentRoute: () => root,
    path: "/inventory",
    beforeLoad: () => {
      throw redirect({ to: "/" }); // the console's landing page before the redesign
    },
  }),
  createRoute({ getParentRoute: () => root, path: "/approvals", component: ApprovalsPage, validateSearch: approvalsSearch }),
  createRoute({
    getParentRoute: () => root,
    path: "/approvals/reviews/$reviewId",
    component: ReviewPage,
    loader: ({ context, params }) => void context.queryClient.prefetchQuery(queries.review(params.reviewId)),
  }),
  createRoute({ getParentRoute: () => root, path: "/servers", component: ServersPage, validateSearch: serversSearch }),
  createRoute({
    getParentRoute: () => root,
    path: "/servers/$serverId",
    component: ServerPage,
    loader: ({ context, params }) => void context.queryClient.prefetchQuery(queries.server(params.serverId)),
  }),
  createRoute({ getParentRoute: () => root, path: "/agents", component: AgentsPage, validateSearch: agentsSearch }),
  createRoute({
    getParentRoute: () => root,
    path: "/agents/$agentId",
    component: AgentPage,
    loader: ({ context, params }) => void context.queryClient.prefetchQuery(queries.agent(params.agentId)),
  }),
  createRoute({ getParentRoute: () => root, path: "/grants", component: GrantsPage, validateSearch: grantsSearch }),
  createRoute({ getParentRoute: () => root, path: "/activity", component: ActivityPage, validateSearch: activitySearch }),
];

export function createAppRouter(queryClient: QueryClient) {
  return createRouter({
    routeTree: root.addChildren(routes),
    context: { queryClient },
    defaultPreload: "intent",
    defaultPreloadStaleTime: 0, // freshness is TanStack Query's job
    scrollRestoration: true,
  });
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createAppRouter>;
  }
}
