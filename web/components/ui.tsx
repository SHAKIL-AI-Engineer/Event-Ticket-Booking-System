"use client";
import { ReactNode, useEffect, useRef } from "react";
import Link from "next/link";
import {
  Loader2,
  X,
  Ticket,
  AlertCircle,
  Check,
  ChevronRight,
} from "lucide-react";
import { useApp } from "./providers";
export function Loading({
  text = "Finding your next moment…",
}: {
  text?: string;
}) {
  return (
    <div className="loading" role="status">
      <Loader2 className="spin" size={26} />
      <span>{text}</span>
    </div>
  );
}
export function ErrorState({
  message,
  retry,
}: {
  message: string;
  retry?: () => void;
}) {
  return (
    <div className="empty-state">
      <AlertCircle size={36} />
      <h2>Let’s try that again</h2>
      <p>{message}</p>
      {retry && (
        <button className="button" onClick={retry}>
          Try again
        </button>
      )}
    </div>
  );
}
export function Empty({
  title,
  text,
  action,
  href,
  icon,
}: {
  title: string;
  text: string;
  action?: string;
  href?: string;
  icon?: ReactNode;
}) {
  return (
    <div className="empty-state">
      {icon || <Ticket size={40} />}
      <h2>{title}</h2>
      <p>{text}</p>
      {href && action && (
        <Link className="button" href={href}>
          {action}
        </Link>
      )}
    </div>
  );
}
export function Badge({ status }: { status: string }) {
  return (
    <span
      className={`badge badge-${status.toLowerCase().replaceAll("_", "-")}`}
    >
      {status.replaceAll("_", " ").toLowerCase()}
    </span>
  );
}
export function Modal({
  title,
  children,
  onClose,
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const dialog = ref.current;
    dialog?.showModal();
    return () => dialog?.close();
  }, []);
  return (
    <dialog
      ref={ref}
      className="modal"
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="modal-header">
        <h2>{title}</h2>
        <button
          className="icon-button"
          onClick={onClose}
          aria-label="Close dialog"
        >
          <X />
        </button>
      </div>
      {children}
    </dialog>
  );
}
export function Field({
  label,
  children,
  hint,
}: {
  label: string;
  children: ReactNode;
  hint?: string;
}) {
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {hint && <small>{hint}</small>}
    </label>
  );
}
export function Breadcrumb({
  items,
}: {
  items: { label: string; href?: string }[];
}) {
  return (
    <nav className="breadcrumb" aria-label="Breadcrumb">
      {items.map((x, i) => (
        <span key={i}>
          {i > 0 && <ChevronRight size={13} />}{" "}
          {x.href ? (
            <Link href={x.href}>{x.label}</Link>
          ) : (
            <span aria-current="page">{x.label}</span>
          )}
        </span>
      ))}
    </nav>
  );
}
export function Guard({
  children,
  roles,
}: {
  children: ReactNode;
  roles?: string[];
}) {
  const { user, loading } = useApp();
  if (loading) return <Loading />;
  if (!user)
    return (
      <Empty
        title="Your experiences, all in one place"
        text="Sign in to manage your tickets and pick up where you left off."
        action="Sign in"
        href="/login"
      />
    );
  if (roles && !roles.includes(user.role))
    return (
      <Empty
        title="This space needs a different role"
        text="Your account doesn’t have access to this area. An administrator can update your role."
        action="My account"
        href="/account"
      />
    );
  return <>{children}</>;
}
export function Steps({ current }: { current: number }) {
  return (
    <div className="steps">
      {["Choose seats", "Checkout", "Your tickets"].map((s, i) => (
        <div key={s} className={i <= current ? "step completed" : "step"}>
          <span>{i < current ? <Check size={14} /> : i + 1}</span>
          {s}
        </div>
      ))}
    </div>
  );
}
