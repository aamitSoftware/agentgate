import type { InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

const control =
  "w-full rounded border border-line-strong bg-canvas px-2.5 text-[13px] text-ink placeholder:text-ink-faint focus:border-info focus:outline-none";

export const Input = ({ className, ...props }: InputHTMLAttributes<HTMLInputElement>) => (
  <input className={cn(control, "h-8", className)} {...props} />
);

export const Textarea = ({ className, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement>) => (
  <textarea className={cn(control, "min-h-16 py-2 leading-snug", className)} {...props} />
);

export const Select = ({ className, ...props }: SelectHTMLAttributes<HTMLSelectElement>) => (
  <select className={cn(control, "h-8", className)} {...props} />
);

export const Label = ({ className, ...props }: React.LabelHTMLAttributes<HTMLLabelElement>) => (
  <label className={cn("mb-1 block text-[11px] font-medium tracking-wide text-ink-muted uppercase", className)} {...props} />
);
