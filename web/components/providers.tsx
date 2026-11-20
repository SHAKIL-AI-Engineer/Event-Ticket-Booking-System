"use client";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  ReactNode,
} from "react";
import { X, CheckCircle2, AlertCircle } from "lucide-react";
import { api } from "@/lib/api";
import type { User, Config } from "@/lib/types";
type Context = {
  user: User | null;
  loading: boolean;
  config: Config | null;
  refresh: () => Promise<void>;
  notify: (text: string, error?: boolean) => void;
  favorites: number[];
  toggleFavorite: (id: number) => Promise<void>;
};
const AppContext = createContext<Context | null>(null);
export function Providers({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null),
    [loading, setLoading] = useState(true),
    [config, setConfig] = useState<Config | null>(null),
    [favorites, setFavorites] = useState<number[]>([]),
    [toast, setToast] = useState<{ text: string; error: boolean } | null>(null);
  const notify = useCallback(
    (text: string, error = false) => setToast({ text, error }),
    [],
  );
  const refresh = useCallback(async () => {
    try {
      const data = await api<{ user: User | null }>("/auth/me");
      setUser(data.user);
      setFavorites(data.user ? await api<number[]>("/favorites") : []);
    } finally {
      setLoading(false);
    }
  }, []);
  useEffect(() => {
    refresh().catch(() =>
      notify("Could not connect to EventHub. Please refresh.", true),
    );
    api<Config>("/config")
      .then(setConfig)
      .catch(() => {});
  }, [refresh, notify]);
  useEffect(() => {
    if (!toast) return;
    const timeout = setTimeout(() => setToast(null), 5000);
    return () => clearTimeout(timeout);
  }, [toast]);
  async function toggleFavorite(id: number) {
    if (!user) {
      notify("Sign in to save your favorite events.", true);
      return;
    }
    const saved = favorites.includes(id);
    try {
      await api(`/favorites/${id}`, { method: saved ? "DELETE" : "PUT" });
      setFavorites((old) =>
        saved ? old.filter((x) => x !== id) : [...old, id],
      );
      notify(
        saved ? "Removed from saved events." : "Added to your saved events.",
      );
    } catch (e) {
      notify((e as Error).message, true);
    }
  }
  return (
    <AppContext.Provider
      value={{
        user,
        loading,
        config,
        refresh,
        notify,
        favorites,
        toggleFavorite,
      }}
    >
      {children}
      {toast && (
        <div
          className={`toast ${toast.error ? "toast-error" : ""}`}
          role={toast.error ? "alert" : "status"}
        >
          {toast.error ? <AlertCircle size={20} /> : <CheckCircle2 size={20} />}
          <span>{toast.text}</span>
          <button
            className="icon-button"
            aria-label="Dismiss notification"
            onClick={() => setToast(null)}
          >
            <X size={17} />
          </button>
        </div>
      )}
    </AppContext.Provider>
  );
}
export function useApp() {
  const value = useContext(AppContext);
  if (!value) throw new Error("Missing Providers");
  return value;
}
