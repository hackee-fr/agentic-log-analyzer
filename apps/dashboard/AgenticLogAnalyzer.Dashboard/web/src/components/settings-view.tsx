import { useCallback, useEffect, useState, type ReactNode } from "react"
import {
  CircleHelp,
  Copy,
  Cpu,
  Database,
  HardDrive,
  RefreshCw,
  Server,
  ShieldCheck,
  TriangleAlert,
} from "lucide-react"
import { toast } from "sonner"
import { apiBase, getApiSettings, getLlmStatus, type ApiSettings, type LlmStatus } from "@/lib/api"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"

type SettingsViewProps = {
  apiConnected: boolean
  eventCount: number
  detectionCount: number
}

export function SettingsView({ apiConnected, eventCount, detectionCount }: SettingsViewProps) {
  const [settings, setSettings] = useState<ApiSettings | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [llm, setLlm] = useState<{ enabled: boolean; status: LlmStatus | null } | null>(null)

  const loadSettings = useCallback(async () => {
    try {
      const [nextSettings, nextLlm] = await Promise.all([getApiSettings(), getLlmStatus().catch(() => null)])
      setSettings(nextSettings)
      setLlm(nextLlm)
      setError(null)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : "Could not load API settings.")
    } finally {
      setLoading(false)
    }
  }, [])

  const refresh = useCallback(async () => {
    setLoading(true)
    await loadSettings()
  }, [loadSettings])

  useEffect(() => {
    let mounted = true
    const load = async () => {
      try {
        const [result, llmResult] = await Promise.all([getApiSettings(), getLlmStatus().catch(() => null)])
        if (!mounted) return
        setSettings(result)
        setLlm(llmResult)
        setError(null)
      } catch (requestError) {
        if (!mounted) return
        setError(requestError instanceof Error ? requestError.message : "Could not load API settings.")
      } finally {
        if (mounted) setLoading(false)
      }
    }
    void load()
    return () => {
      mounted = false
    }
  }, [])

  const copy = async (value: string, label: string) => {
    try {
      await navigator.clipboard.writeText(value)
      toast.success(`${label} copied`)
    } catch {
      toast.error("Clipboard unavailable", { description: "Select and copy the value manually." })
    }
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-center">
        <div>
          <h2 className="text-sm font-semibold text-foreground">Runtime configuration</h2>
          <p className="mt-1 text-xs text-muted-foreground">Values reported by the API process. Change environment variables and restart the API to apply configuration changes.</p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void refresh()} disabled={loading} className="h-9 gap-2 self-start text-xs sm:self-auto">
          <RefreshCw className={`size-3.5 ${loading ? "animate-spin" : ""}`} />Refresh settings
        </Button>
      </div>

      {error && <div role="alert" className="flex items-center gap-3 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3 text-xs text-rose-300"><TriangleAlert className="size-4 shrink-0" />Could not read settings: {error}</div>}

      <section className="grid gap-4 xl:grid-cols-3">
        <StatusCard
          icon={Server}
          title="API service"
          detail={apiConnected ? "The dashboard can reach the API." : "The API is not responding."}
          badge={apiConnected ? "Connected" : "Unavailable"}
          tone={apiConnected ? "green" : "rose"}
        >
          <SettingRow label="Endpoint" value={apiBase || window.location.origin} />
          <SettingRow label="Environment" value={loading ? "Loading…" : settings?.environment ?? "Unavailable"} />
        </StatusCard>

        <StatusCard
          icon={Database}
          title="Event storage"
          detail="Canonical events persist between API restarts."
          badge={loading ? "Checking" : settings?.storage.provider ?? "Unknown"}
          tone={settings?.storage.provider === "sqlite" ? "blue" : "slate"}
        >
          <SettingRow label="Database file" value={loading ? "Loading…" : settings?.storage.fileName ?? "Unavailable"} />
          <SettingRow label="Status" value={loading ? "Loading…" : settings?.storage.initialized ? "Initialized" : "Created on first use"} />
          <SettingRow label="Events available" value={eventCount.toLocaleString("en-US")} />
        </StatusCard>

        <StatusCard
          icon={Cpu}
          title="Analysis engine"
          detail="Deterministic analysis is the source of truth."
          badge={loading ? "Checking" : settings?.analysis.deterministicEngineEnabled ? "Ready" : "Unavailable"}
          tone={settings?.analysis.deterministicEngineEnabled ? "green" : "slate"}
        >
          <SettingRow label="Deterministic rules" value={loading ? "Loading…" : settings?.analysis.deterministicEngineEnabled ? "Enabled" : "Disabled"} />
          <SettingRow label="Active findings" value={detectionCount.toLocaleString("en-US")} />
          <SettingRow label="LLM" value={loading ? "Loading…" : settings?.analysis.llmEnabled ? `${settings.analysis.llmProvider} · ${settings.analysis.llmModel}` : "Disabled"} />
        </StatusCard>
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="px-5 pt-5 pb-3">
            <div className="flex items-center gap-2"><HardDrive className="size-4 text-blue-400" /><CardTitle className="text-[13px]">Storage configuration</CardTitle></div>
            <CardDescription className="text-[11px]">API and worker should point to the same SQLite file.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 px-5 pb-5">
            <ConfigSnippet
              title="Default development setup"
              value={'Storage__Provider=sqlite\nStorage__SqlitePath=data/events.sqlite3'}
              onCopy={() => void copy('Storage__Provider=sqlite\nStorage__SqlitePath=data/events.sqlite3', "Storage settings")}
            />
            <div className="flex gap-2 rounded-lg border border-border bg-muted/40 px-3 py-2.5 text-[11px] leading-5 text-foreground/70"><CircleHelp className="mt-0.5 size-3.5 shrink-0 text-muted-foreground" />A new SQLite database imports events from existing JSONL files once. The source files remain unchanged, and emptying the database does not import them again.</div>
          </CardContent>
        </Card>

        <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="px-5 pt-5 pb-3">
            <div className="flex items-center gap-2"><ShieldCheck className="size-4 text-emerald-400" /><CardTitle className="text-[13px]">LLM provider</CardTitle></div>
            <CardDescription className="text-[11px]">Optional. It can rephrase answers; deterministic evidence remains authoritative.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 px-5 pb-5">
            <div className="space-y-2 rounded-lg border border-border px-3 py-2.5">
              <div className="flex items-center justify-between">
                <span className="text-xs text-foreground/70">Provider status</span>
                <LlmBadge loading={loading} llm={llm} />
              </div>
              {llm?.status && (
                <div className="space-y-1 border-t border-border pt-2 text-[11px] text-muted-foreground">
                  <div className="flex justify-between gap-3"><span>Model</span><span className="font-mono text-foreground/80">{llm.status.model}</span></div>
                  <div className="flex justify-between gap-3"><span>Installed models</span><span className="truncate font-mono text-foreground/80">{llm.status.installedModels.length ? llm.status.installedModels.join(", ") : "None"}</span></div>
                  {llm.status.error && <div className="flex gap-2 rounded-md border border-amber-500/30 bg-amber-500/10 px-2 py-1.5 text-amber-300"><TriangleAlert className="mt-0.5 size-3 shrink-0" />{llm.status.error}</div>}
                </div>
              )}
            </div>
            <ConfigSnippet
              title="1 · Start Ollama in Docker and pull the model"
              value={OLLAMA_DOCKER_COMMAND}
              onCopy={() => void copy(OLLAMA_DOCKER_COMMAND, "Docker command")}
            />
            <ConfigSnippet
              title="2 · Start the API with Ollama enabled"
              value={OLLAMA_API_ENV}
              onCopy={() => void copy(OLLAMA_API_ENV, "Ollama settings")}
            />
          </CardContent>
        </Card>
      </section>

      <p className="text-[11px] text-muted-foreground">Settings are read-only in this dashboard. Apply changes in your local environment configuration, then restart the API.</p>
    </div>
  )
}

const OLLAMA_DOCKER_COMMAND = "docker compose -f infrastructure/docker/docker-compose.yml up -d ollama ollama-pull"
const OLLAMA_API_ENV = "Llm__Provider=ollama\nLlm__Ollama__BaseUrl=http://localhost:11434\nLlm__Ollama__Model=llama3.2"

function LlmBadge({ loading, llm }: { loading: boolean; llm: { enabled: boolean; status: LlmStatus | null } | null }) {
  const [label, tone] = loading
    ? ["Checking", "border-border bg-muted/40 text-foreground/70"]
    : !llm?.enabled
      ? ["Not configured", "border-border bg-muted/40 text-foreground/70"]
      : llm.status?.modelAvailable
        ? ["Ready", "border-emerald-500/30 bg-emerald-500/10 text-emerald-400"]
        : llm.status?.reachable
          ? ["Model missing", "border-amber-500/30 bg-amber-500/10 text-amber-400"]
          : ["Unreachable", "border-rose-500/30 bg-rose-500/10 text-rose-400"]
  return <Badge variant="outline" className={tone}>{label}</Badge>
}

function StatusCard({
  icon: Icon,
  title,
  detail,
  badge,
  tone,
  children,
}: {
  icon: typeof Server
  title: string
  detail: string
  badge: string
  tone: "green" | "rose" | "blue" | "slate"
  children: ReactNode
}) {
  const tones = {
    green: "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",
    rose: "border-rose-500/30 bg-rose-500/10 text-rose-400",
    blue: "border-blue-500/30 bg-blue-500/10 text-blue-400",
    slate: "border-border bg-muted/40 text-foreground/70",
  }
  return (
    <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
      <CardHeader className="px-5 pt-5 pb-3">
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-center gap-2"><Icon className="size-4 text-blue-400" /><CardTitle className="text-[13px]">{title}</CardTitle></div>
          <Badge variant="outline" className={tones[tone]}>{badge}</Badge>
        </div>
        <CardDescription className="text-[11px]">{detail}</CardDescription>
      </CardHeader>
      <CardContent className="space-y-2 px-5 pb-5">{children}</CardContent>
    </Card>
  )
}

function SettingRow({ label, value }: { label: string; value: string }) {
  return <div className="flex items-center justify-between gap-3 border-t border-border pt-2 text-[11px]"><span className="text-muted-foreground">{label}</span><span className="max-w-[65%] truncate text-right font-medium text-foreground/90" title={value}>{value}</span></div>
}

function ConfigSnippet({ title, value, onCopy }: { title: string; value: string; onCopy: () => void }) {
  return (
    <div className="overflow-hidden rounded-lg border border-border">
      <div className="flex items-center justify-between border-b border-border bg-muted/40 px-3 py-2"><span className="text-[10px] font-medium text-foreground/70">{title}</span><Button type="button" variant="ghost" size="sm" onClick={onCopy} className="h-7 gap-1.5 px-2 text-[10px] text-muted-foreground"><Copy className="size-3" />Copy</Button></div>
      <pre className="overflow-x-auto bg-card px-3 py-2.5 font-mono text-[10px] leading-5 text-foreground/70">{value}</pre>
    </div>
  )
}
