// Barcode scanner listener hook for POS cashier checkout

import { useEffect, useRef } from "react";

export interface UsePosScannerOptions {
  readonly onScan: (barcode: string) => void;
  readonly enabled?: boolean;
  readonly minLength?: number;
  readonly maxIntervalMs?: number;
}

export function usePosScanner({
  onScan,
  enabled = true,
  minLength = 3,
  maxIntervalMs = 60,
}: UsePosScannerOptions) {
  const bufferRef = useRef<string>("");
  const lastKeyTimeRef = useRef<number>(0);

  useEffect(() => {
    if (!enabled) return;

    const handleKeyDown = (event: KeyboardEvent) => {
      // Ignore if user is typing inside an explicit input or textarea other than the barcode input
      const target = event.target as HTMLElement | null;
      const isInput = target?.tagName === "INPUT" || target?.tagName === "TEXTAREA";
      const isBarcodeInput = target?.dataset?.testid === "pos-barcode-input";

      // If user is typing in regular inputs (like search dialog or discount modal), don't intercept globally
      if (isInput && !isBarcodeInput) {
        return;
      }

      const now = Date.now();
      const timeDiff = now - lastKeyTimeRef.current;
      lastKeyTimeRef.current = now;

      if (event.key === "Enter") {
        const barcode = bufferRef.current.trim();
        bufferRef.current = "";

        if (barcode.length >= minLength) {
          event.preventDefault();
          onScan(barcode);
        }
        return;
      }

      // If key is a printable character (length 1)
      if (event.key.length === 1 && !event.ctrlKey && !event.altKey && !event.metaKey) {
        // If interval between keys is too long, reset buffer (it was human typing, not a scanner)
        if (timeDiff > maxIntervalMs && !isBarcodeInput) {
          bufferRef.current = "";
        }
        bufferRef.current += event.key;
      }
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => {
      window.removeEventListener("keydown", handleKeyDown);
    };
  }, [enabled, onScan, minLength, maxIntervalMs]);
}
