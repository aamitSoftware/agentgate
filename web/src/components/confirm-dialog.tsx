import { useId, useState, type ReactNode } from "react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field";
import { Spinner } from "@/components/ui/spinner";
import { Textarea } from "@/components/ui/textarea";

/**
 * Confirms an irreversible governance action (console-ui skill): says what will happen, requires a reason
 * (it goes into the audit trail), and stays open with the error if the service refuses.
 */
export function ConfirmDialog({
  trigger,
  title,
  consequence,
  confirmLabel,
  reasonLabel = "Reason",
  initialReason = "",
  onConfirm,
}: {
  trigger: ReactNode;
  title: string;
  consequence: ReactNode;
  confirmLabel: string;
  reasonLabel?: string;
  initialReason?: string;
  onConfirm: (reason: string) => Promise<unknown>;
}) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState(initialReason);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const reasonId = useId();

  const openChange = (next: boolean) => {
    if (pending) return;
    setOpen(next);
    if (next) {
      setReason(initialReason);
      setError(null);
    }
  };

  const confirm = async () => {
    setPending(true);
    setError(null);
    try {
      await onConfirm(reason.trim());
      setOpen(false);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : "The service refused the action.");
    } finally {
      setPending(false);
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={openChange}>
      <AlertDialogTrigger asChild>{trigger}</AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{consequence}</AlertDialogDescription>
        </AlertDialogHeader>
        <Field>
          <FieldLabel htmlFor={reasonId}>{reasonLabel}</FieldLabel>
          <Textarea id={reasonId} name="reason" autoComplete="off" value={reason} onChange={(event) => setReason(event.target.value)} />
          <FieldDescription>Required. Recorded in the audit trail with your name.</FieldDescription>
        </Field>
        {error && (
          <Alert variant="destructive" role="alert">
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending}>Cancel</AlertDialogCancel>
          <Button variant="destructive" disabled={!reason.trim() || pending} onClick={() => void confirm()}>
            {pending && <Spinner data-icon="inline-start" />}
            {confirmLabel}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
