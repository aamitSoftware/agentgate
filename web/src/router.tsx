import { createRootRoute, createRoute, createRouter, redirect } from "@tanstack/react-router";
import { Shell } from "@/components/shell";
import { ActivityPage } from "@/routes/activity";
import { AgentPage } from "@/routes/agent";
import { ApprovalsPage } from "@/routes/approvals";
import { InventoryPage } from "@/routes/inventory";
import { ReviewPage } from "@/routes/review";
import { ServerPage } from "@/routes/server";

const root = createRootRoute({ component: Shell, notFoundComponent: () => <p className="p-5 text-ink-muted">Not found.</p> });

const routes = [
  createRoute({
    getParentRoute: () => root,
    path: "/",
    beforeLoad: () => {
      throw redirect({ to: "/inventory" });
    },
  }),
  createRoute({ getParentRoute: () => root, path: "/inventory", component: InventoryPage }),
  createRoute({ getParentRoute: () => root, path: "/approvals", component: ApprovalsPage }),
  createRoute({ getParentRoute: () => root, path: "/approvals/reviews/$reviewId", component: ReviewPage }),
  createRoute({ getParentRoute: () => root, path: "/activity", component: ActivityPage }),
  createRoute({ getParentRoute: () => root, path: "/servers/$serverId", component: ServerPage }),
  createRoute({ getParentRoute: () => root, path: "/agents/$agentId", component: AgentPage }),
];

export const router = createRouter({ routeTree: root.addChildren(routes), defaultPreload: "intent" });

declare module "@tanstack/react-router" {
  interface Register {
    router: typeof router;
  }
}
