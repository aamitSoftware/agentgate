import { Link, Outlet, type LinkProps } from "@tanstack/react-router";
import {
  Activity,
  Bot,
  ChevronsUpDown,
  ClipboardCheck,
  Eye,
  KeyRound,
  LayoutDashboard,
  LogOut,
  Menu,
  Monitor,
  Moon,
  Search,
  Server,
  Sun,
  type LucideIcon,
} from "lucide-react";
import { useState } from "react";
import { useGrantQueue, useReviewQueue } from "@/api/queries";
import { useAuth } from "@/auth/auth";
import { BrandMark } from "@/components/brand";
import { CommandPalette } from "@/components/command-palette";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Kbd } from "@/components/ui/kbd";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { useTheme, type ThemePreference } from "@/lib/theme";

interface NavEntry {
  to: LinkProps["to"];
  label: string;
  icon: LucideIcon;
  exact?: boolean;
}

const sections: { heading?: string; entries: NavEntry[] }[] = [
  { entries: [{ to: "/", label: "Overview", icon: LayoutDashboard, exact: true }] },
  { heading: "Governance", entries: [{ to: "/approvals", label: "Approvals", icon: ClipboardCheck }] },
  {
    heading: "Inventory",
    entries: [
      { to: "/servers", label: "Servers", icon: Server },
      { to: "/agents", label: "Agents", icon: Bot },
      { to: "/grants", label: "Grants", icon: KeyRound },
    ],
  },
  { heading: "Monitoring", entries: [{ to: "/activity", label: "Live activity", icon: Activity }] },
];

function useWaitingCount(): number | null {
  const reviews = useReviewQueue();
  const grants = useGrantQueue();
  if (!reviews.data || !grants.data) return null; // loading or not readable: show nothing rather than a false 0
  return reviews.data.length + grants.data.length;
}

function Navigation({ onNavigate }: { onNavigate?: () => void }) {
  const waiting = useWaitingCount();
  return (
    <nav aria-label="Main" className="flex flex-col gap-4">
      {sections.map((section, index) => (
        <div key={section.heading ?? index} className="flex flex-col gap-0.5">
          {section.heading && <h2 className="px-2.5 pb-1 text-xs font-medium text-muted-foreground">{section.heading}</h2>}
          {section.entries.map((entry) => (
            <Link
              key={entry.label}
              to={entry.to}
              activeOptions={{ exact: entry.exact }}
              onClick={onNavigate}
              className="flex h-8 items-center gap-2.5 rounded-md px-2.5 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground data-[status=active]:bg-accent data-[status=active]:font-medium data-[status=active]:text-accent-foreground"
            >
              <entry.icon aria-hidden="true" className="size-4" />
              {entry.label}
              {entry.to === "/approvals" && !!waiting && (
                <span className="ml-auto rounded-sm bg-warning-subtle px-1.5 text-xs font-medium text-warning tabular" aria-label={`${waiting} waiting`}>
                  {waiting}
                </span>
              )}
            </Link>
          ))}
        </div>
      ))}
    </nav>
  );
}

const themeOptions: { value: ThemePreference; label: string; icon: LucideIcon }[] = [
  { value: "system", label: "System", icon: Monitor },
  { value: "light", label: "Light", icon: Sun },
  { value: "dark", label: "Dark", icon: Moon },
];

function UserMenu() {
  const { identity, roles, signOut } = useAuth();
  const { preference, setPreference } = useTheme();
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="h-auto w-full justify-start gap-2.5 px-2 py-1.5 text-left">
          <span className="grid size-7 shrink-0 place-items-center rounded-full bg-accent text-xs font-semibold text-accent-foreground uppercase">
            {identity.username.slice(0, 2)}
          </span>
          <span className="flex min-w-0 flex-col">
            <span className="truncate text-sm font-medium">{identity.username}</span>
            <span className="truncate text-xs font-normal text-muted-foreground">{roles.join(", ") || "no console role"}</span>
          </span>
          <ChevronsUpDown aria-hidden="true" className="ml-auto size-4 text-muted-foreground" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" side="top" className="w-60">
        <DropdownMenuLabel className="flex flex-col">
          <span>{identity.username}</span>
          <span className="text-xs font-normal text-muted-foreground">{roles.join(", ") || "no console role"}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuLabel className="text-xs font-normal text-muted-foreground">Theme</DropdownMenuLabel>
        <DropdownMenuRadioGroup value={preference} onValueChange={(value) => setPreference(value as ThemePreference)}>
          {themeOptions.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              <option.icon aria-hidden="true" />
              {option.label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <DropdownMenuGroup>
          <DropdownMenuItem onSelect={signOut}>
            <LogOut aria-hidden="true" />
            Sign out
          </DropdownMenuItem>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

function SearchButton({ onOpen }: { onOpen: () => void }) {
  return (
    <Button variant="outline" className="w-full justify-start gap-2 px-2.5 text-muted-foreground" onClick={onOpen}>
      <Search data-icon="inline-start" />
      Search
      <Kbd className="ml-auto">⌘&nbsp;K</Kbd>
    </Button>
  );
}

function Brand() {
  return (
    <Link to="/" className="flex items-center gap-2.5 rounded-md">
      <BrandMark />
      <span className="font-semibold tracking-tight">AgentGate</span>
      <span className="rounded-sm border px-1 text-xs text-muted-foreground">dev</span>
    </Link>
  );
}

export function Shell() {
  const { readOnly } = useAuth();
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <div className="grid min-h-svh grid-cols-[14.5rem_minmax(0,1fr)] max-md:grid-cols-1 max-md:grid-rows-[auto_1fr]">
      <a
        href="#main"
        className="sr-only z-50 rounded-md bg-primary px-3 py-2 text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        Skip to content
      </a>

      <aside className="sticky top-0 flex h-svh flex-col gap-5 overflow-y-auto border-r bg-sidebar p-3 max-md:hidden">
        <div className="px-1.5 pt-1.5">
          <Brand />
        </div>
        <SearchButton onOpen={() => setPaletteOpen(true)} />
        <Navigation />
        <div className="mt-auto border-t pt-3">
          <UserMenu />
        </div>
      </aside>

      <header className="sticky top-0 z-30 flex h-14 items-center gap-3 border-b bg-background/95 px-4 backdrop-blur-sm md:hidden">
        <Sheet open={menuOpen} onOpenChange={setMenuOpen}>
          <SheetTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Open navigation">
              <Menu />
            </Button>
          </SheetTrigger>
          <SheetContent side="left" className="flex w-72 flex-col gap-5 p-3">
            <SheetHeader className="p-1.5">
              <SheetTitle asChild>
                <div>
                  <Brand />
                </div>
              </SheetTitle>
              <SheetDescription className="sr-only">Navigation</SheetDescription>
            </SheetHeader>
            <Navigation onNavigate={() => setMenuOpen(false)} />
            <div className="mt-auto border-t pt-3">
              <UserMenu />
            </div>
          </SheetContent>
        </Sheet>
        <Brand />
        <Button variant="ghost" size="icon" className="ml-auto" aria-label="Search" onClick={() => setPaletteOpen(true)}>
          <Search />
        </Button>
      </header>

      <main id="main" tabIndex={-1} className="min-w-0 focus:outline-none">
        {readOnly && (
          <div className="flex items-center gap-2 border-b bg-info-subtle px-4 py-2 text-sm text-info sm:px-6" role="status">
            <Eye aria-hidden="true" className="size-4" />
            Read-only: your role can view everything here and change nothing.
          </div>
        )}
        <Outlet />
      </main>

      <CommandPalette open={paletteOpen} onOpenChange={setPaletteOpen} />
    </div>
  );
}
