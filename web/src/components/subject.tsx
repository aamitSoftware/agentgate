import type { ActivityItem } from "@/api/types";
import { ActivityRow, Timeline } from "@/components/activity";
import { Message } from "@/components/query-state";
import { Section } from "@/components/section";
import { groupRepeats } from "@/lib/activity";

// Sections shared by the server and agent pages.

export function RecentCalls({ calls }: { calls: ActivityItem[] }) {
  return (
    <Section title="Recent calls" description="Gateway decisions for this subject, newest first. Times are UTC.">
      {calls.length === 0 ? (
        <Message title="No calls yet">Tool calls through the gateway appear here, allowed or denied.</Message>
      ) : (
        <ul aria-label="Recent calls">
          {groupRepeats(calls).map((group) => (
            <ActivityRow key={group.item.id} item={group.item} repeats={group.repeats} />
          ))}
        </ul>
      )}
    </Section>
  );
}

export function TimelineSection({ items, what }: { items: ActivityItem[]; what: string }) {
  return (
    <Section title="Timeline">
      {items.length === 0 ? <Message title="Nothing recorded yet">Every governance change to this {what} is recorded here.</Message> : <Timeline items={items} />}
    </Section>
  );
}
