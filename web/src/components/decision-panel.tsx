import { Info, ShieldCheck } from "lucide-react";
import { useId, useState, type ReactNode } from "react";
import { toast } from "sonner";
import { useAuth } from "@/auth/auth";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { Seals } from "@/components/seals";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";

export type Verb = "approve" | "reject";

/**
 * Four-eyes decision on a manifest review or a grant request: the two seals, why approving is or is not
 * possible right now (in words, not a tooltip), a comment, approve, and a confirmed reject.
 */
export function DecisionPanel({
  subject,
  firstApproverId,
  complete,
  undecided,
  closedText,
  highRisk = false,
  finalApproveLabel,
  finalNote,
  rejectConsequence,
  onDecide,
}: {
  /** What is being decided, for messages: "the manifest", "the grant". */
  subject: string;
  firstApproverId: string | null;
  complete: boolean;
  undecided: boolean;
  closedText: string;
  highRisk?: boolean;
  finalApproveLabel: string;
  finalNote?: ReactNode;
  rejectConsequence: ReactNode;
  onDecide: (verb: Verb, comment: string) => Promise<unknown>;
}) {
  const { identity, canReview } = useAuth();
  const [comment, setComment] = useState("");
  const [approving, setApproving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const commentId = useId();

  const gaveFirstApproval = firstApproverId === identity.sub;
  const isFinal = !!firstApproverId;
  const blockedBecause = gaveFirstApproval
    ? "You gave the first approval. The second must come from a different reviewer (four-eyes)."
    : highRisk && !comment.trim()
      ? `Scanned as high risk: explain why ${subject} is acceptable before approving.`
      : null;

  const approve = async () => {
    setApproving(true);
    setError(null);
    try {
      await onDecide("approve", comment.trim());
      setComment("");
      toast.success(isFinal ? `Approved by two reviewers: ${subject} is in force.` : "First approval recorded. A second reviewer must approve.");
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "The approval was refused.");
    } finally {
      setApproving(false);
    }
  };

  const reject = async (reason: string) => {
    await onDecide("reject", reason);
    setComment("");
    toast.success(`Rejected: ${subject} will not be enforced.`);
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center gap-3">
        <Seals firstApproverId={firstApproverId} complete={complete} size="lg" />
        <p className="text-sm text-muted-foreground">
          {complete ? "Approved by two reviewers." : isFinal ? "One approval. A different reviewer gives the second." : "Needs two approvals from different reviewers."}
        </p>
      </div>

      {!undecided ? (
        <p className="text-sm text-muted-foreground">{closedText}</p>
      ) : !canReview ? (
        <Alert variant="info">
          <Info />
          <AlertDescription>Security reviewers decide. You can follow the decision here as it happens.</AlertDescription>
        </Alert>
      ) : (
        <>
          <Field>
            <FieldLabel htmlFor={commentId}>{highRisk ? "Why is this acceptable?" : "Comment"}</FieldLabel>
            <Textarea
              id={commentId}
              name="comment"
              autoComplete="off"
              placeholder={highRisk ? "What you checked, and why the findings don't apply…" : "Optional for approval…"}
              value={comment}
              onChange={(event) => setComment(event.target.value)}
            />
            <FieldDescription>{highRisk ? "Required for each approval of a high-risk manifest." : "Recorded in the audit trail."}</FieldDescription>
          </Field>

          {blockedBecause && (
            <p className="flex items-start gap-2 text-sm text-muted-foreground">
              <ShieldCheck aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
              {blockedBecause}
            </p>
          )}

          <div className="flex flex-wrap gap-2">
            <Button disabled={!!blockedBecause || approving} onClick={() => void approve()}>
              {approving && <Spinner data-icon="inline-start" />}
              {isFinal ? finalApproveLabel : "Approve"}
            </Button>
            <ConfirmDialog
              trigger={
                <Button variant="outline" disabled={approving}>
                  Reject…
                </Button>
              }
              title={`Reject ${subject}?`}
              consequence={rejectConsequence}
              confirmLabel="Reject"
              reasonLabel="Why are you rejecting it?"
              initialReason={comment}
              onConfirm={reject}
            />
          </div>
          {isFinal && !gaveFirstApproval && finalNote && <p className="text-sm text-muted-foreground">{finalNote}</p>}
          {error && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </>
      )}
    </div>
  );
}
