/** "PartiallyApproved" → "Partially approved": enum names from the services, as words. */
export function humanize(name: string): string {
  const words = name.replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase();
  return words.charAt(0).toUpperCase() + words.slice(1);
}
