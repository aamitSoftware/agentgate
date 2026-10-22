import { QueryCache, QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { toast } from "sonner";
import { ApiError } from "@/api/client";
import { AuthProvider } from "@/auth/auth";
import { Toaster } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { ThemeProvider } from "@/lib/theme";
import { createAppRouter } from "@/router";
import "@/styles/index.css";

const queryClient = new QueryClient({
  queryCache: new QueryCache({
    // A first load that fails shows its error in place (QueryState); a background refresh that fails while
    // older data is on screen says so once, instead of silently showing stale data.
    onError: (error, query) => {
      if (query.state.data !== undefined && !(error instanceof ApiError && error.status === 401))
        toast.error("Couldn't refresh; the data on screen may be out of date.", { id: "refresh-failed", description: error.message });
    },
  }),
  defaultOptions: {
    queries: {
      // a 4xx is an answer (not found, forbidden), not a glitch: retrying it only delays the message
      retry: (failures, error) => !(error instanceof ApiError && error.status < 500) && failures < 2,
      refetchOnWindowFocus: false,
      staleTime: 2_000,
    },
  },
});

const router = createAppRouter(queryClient);

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ThemeProvider>
      <TooltipProvider delayDuration={300}>
        <AuthProvider>
          <QueryClientProvider client={queryClient}>
            <RouterProvider router={router} />
          </QueryClientProvider>
        </AuthProvider>
        <Toaster position="bottom-right" />
      </TooltipProvider>
    </ThemeProvider>
  </StrictMode>,
);
