/**
 * A manifest hash drawn as colour cells (web/DESIGN.md, "Manifest fingerprint"), so two manifests can be
 * told apart at a glance. Each cell's hue comes from two hex digits of the hash; lightness and chroma are
 * fixed and low, so a fingerprint reads as identity and never as a status colour.
 */
export function fingerprint(hash: string, cells = 6): string[] {
  const hex = hash.replace(/[^0-9a-f]/gi, "").toLowerCase();
  return Array.from({ length: cells }, (_, index) => {
    const byte = parseInt(hex.slice(index * 2, index * 2 + 2) || "0", 16);
    return `oklch(0.7 0.11 ${Math.round((byte / 256) * 360)})`;
  });
}
