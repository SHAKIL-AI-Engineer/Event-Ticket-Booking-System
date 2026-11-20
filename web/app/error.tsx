"use client";
import { ErrorState } from "@/components/ui";
export default function Error({
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  return (
    <div className="container page">
      <ErrorState
        message="We couldn’t load this page. Your booking is saved; try reloading."
        retry={reset}
      />
    </div>
  );
}
