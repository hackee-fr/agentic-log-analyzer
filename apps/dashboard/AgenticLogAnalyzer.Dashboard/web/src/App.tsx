import { useCallback, useEffect, useMemo, useState } from "react"
import {
  Activity,
  AlertTriangle,
  ArrowRight,
  Bell,
  Bot,
  Check,
  ChevronLeft,
  ChevronRight,
  Database,
  FileClock,
  FileSearch,
  Fingerprint,
  Globe2,
  Layers3,
  LoaderCircle,
  Network,
  RefreshCw,
  Search,
  Server,
  Shield,
  ShieldAlert,
  Sparkles,
  Upload,
  UserRound,
  X,
} from "lucide-react"
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts"
import { toast } from "sonner"
import {
  getEvents,
  ingestLogs,
  runInvestigation,
  type CanonicalEvent,
  type InvestigationReport,
} from "@/lib/api"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Separator } from "@/components/ui/separator"
import { LogChat } from "@/components/log-chat"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"

type View = "overview" | "assistant" | "investigations" | "events" | "sources"

const navigation: { id: View; label: string; icon: typeof Activity }[] = [
  { id: "overview", label: "Overview", icon: Layers3 },
  { id: "assistant", label: "Assistant", icon: Bot },
  { id: "investigations", label: "Investigations", icon: ShieldAlert },
  { id: "events", label: "Events", icon: FileSearch },
  { id: "sources", label: "Sources", icon: Database },
]

const formatTimestamp = (value: string) =>
  new Intl.DateTimeFormat("en-GB", {
    dateStyle: "medium",
    timeStyle: "medium",
  }).format(new Date(value))

const formatShortTime = (value: string) =>
  new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit" }).format(
    new Date(value),
  )

const resultTone = (result: string | null) => {
  const value = result?.toLowerCase() ?? ""
  if (value.includes("fail") || value.includes("error")) return "danger"
  if (value.includes("warn")) return "warning"
  if (value.includes("success")) return "success"
  return "neutral"
}

const fetchDashboardData = async () =>
  Promise.all([getEvents(), runInvestigation("", 1000)])

function ResultBadge({ result }: { result: string | null }) {
  const tone = resultTone(result)
  const colors = {
    danger: "border-rose-200 bg-rose-50 text-rose-700",
    warning: "border-amber-200 bg-amber-50 text-amber-700",
    success: "border-emerald-200 bg-emerald-50 text-emerald-700",
    neutral: "border-slate-200 bg-slate-50 text-slate-600",
  }
  return (
    <Badge variant="outline" className={`rounded-full px-2.5 py-1 font-medium ${colors[tone]}`}>
      <span
        className={`mr-1.5 size-1.5 rounded-full ${
          tone === "danger"
            ? "bg-rose-500"
            : tone === "warning"
              ? "bg-amber-500"
              : tone === "success"
                ? "bg-emerald-500"
                : "bg-slate-400"
        }`}
      />
      {result || "event"}
    </Badge>
  )
}

function App() {
  const [events, setEvents] = useState<CanonicalEvent[]>([])
  const [report, setReport] = useState<InvestigationReport | null>(null)
  const [activeView, setActiveView] = useState<View>("overview")
  const [query, setQuery] = useState("")
  const [connected, setConnected] = useState(false)
  const [loading, setLoading] = useState(true)
  const [investigating, setInvestigating] = useState(false)
  const [importing, setImporting] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [fileDropActive, setFileDropActive] = useState(false)
  const [content, setContent] = useState("")
  const [source, setSource] = useState("manual-import")

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      const [nextEvents, nextReport] = await fetchDashboardData()
      setEvents(nextEvents)
      setReport(nextReport)
      setConnected(true)
    } catch (error) {
      setConnected(false)
      toast.error(error instanceof Error ? error.message : "API unavailable")
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let mounted = true
    const load = async () => {
      try {
        const [nextEvents, nextReport] = await fetchDashboardData()
        if (!mounted) return
        setEvents(nextEvents)
        setReport(nextReport)
        setConnected(true)
      } catch (error) {
        if (!mounted) return
        setConnected(false)
        toast.error(error instanceof Error ? error.message : "API unavailable")
      } finally {
        if (mounted) setLoading(false)
      }
    }
    void load()
    return () => {
      mounted = false
    }
  }, [])

  const visibleEvents = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase()
    if (!normalized) return events
    return events.filter((event) =>
      [
        event.rawContent,
        event.category,
        event.action,
        event.result,
        event.user,
        event.device,
        event.sourceIp,
        event.sourceName,
      ].some((value) => value?.toLocaleLowerCase().includes(normalized)),
    )
  }, [events, query])

  const activityData = useMemo(() => {
    const buckets = new Map<string, { label: string; events: number; failures: number; order: number }>()
    for (const event of events) {
      const date = new Date(event.timestamp)
      date.setSeconds(0, 0)
      const key = date.toISOString()
      const bucket = buckets.get(key) ?? {
        label: formatShortTime(event.timestamp),
        events: 0,
        failures: 0,
        order: date.getTime(),
      }
      bucket.events += 1
      if (resultTone(event.result) === "danger") bucket.failures += 1
      buckets.set(key, bucket)
    }
    return [...buckets.values()].sort((a, b) => a.order - b.order).slice(-24)
  }, [events])

  const categoryData = useMemo(() => {
    const counts = new Map<string, number>()
    events.forEach((event) => counts.set(event.category, (counts.get(event.category) ?? 0) + 1))
    return [...counts.entries()]
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 6)
  }, [events])

  const sources = useMemo(() => {
    const counts = new Map<string, { name: string; events: number; types: Set<string>; latest: string }>()
    for (const event of events) {
      const existing = counts.get(event.sourceName) ?? {
        name: event.sourceName,
        events: 0,
        types: new Set<string>(),
        latest: event.timestamp,
      }
      existing.events += 1
      existing.types.add(event.sourceType)
      if (new Date(event.timestamp) > new Date(existing.latest)) existing.latest = event.timestamp
      counts.set(event.sourceName, existing)
    }
    return [...counts.values()].sort((a, b) => b.events - a.events)
  }, [events])

  const failureCount = events.filter((event) => resultTone(event.result) === "danger").length
  const detectionCount = report?.detections.length ?? 0

  const startInvestigation = async () => {
    setInvestigating(true)
    try {
      const result = await runInvestigation(query.trim(), 1000)
      setReport(result)
      setActiveView("investigations")
      toast.success("Investigation completed", {
        description: `${result.events.length} events reviewed · ${result.detections.length} detection(s)`,
      })
    } catch (error) {
      toast.error("Investigation failed", {
        description: error instanceof Error ? error.message : "Check that the API is running.",
      })
    } finally {
      setInvestigating(false)
    }
  }

  const importFile = async (file?: File) => {
    if (!file) return
    if (!/\.(log|txt)$/i.test(file.name) && file.type !== "text/plain") {
      toast.error("Unsupported file type", { description: "Choose a .log or .txt text file." })
      return
    }
    try {
      setSource(file.name)
      setContent(await file.text())
    } catch {
      toast.error("Could not read this file", { description: "Try another .log or .txt file." })
    }
  }

  const submitImport = async () => {
    if (!content.trim()) {
      toast.error("Choose a file or paste log lines.")
      return
    }
    setImporting(true)
    try {
      const result = await ingestLogs(content, source.trim() || "manual-import")
      setImportOpen(false)
      setContent("")
      await refresh()
      toast.success(`${result.accepted} event(s) imported`, {
        description: result.rejected.length
          ? `${result.rejected.length} row(s) rejected. First error: ${result.rejected[0]?.reason}`
          : "All lines were normalized.",
      })
    } catch (error) {
      toast.error("Import impossible", {
        description: error instanceof Error ? error.message : "Check that the API is running.",
      })
    } finally {
      setImporting(false)
    }
  }

  const viewTitle = {
    overview: "Overview",
    assistant: "Log assistant",
    investigations: "Investigations",
    events: "Events",
    sources: "Log sources",
  }[activeView]

  return (
    <div className="min-h-screen bg-[#f5f7fb] text-slate-900">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-[248px] flex-col border-r border-slate-800 bg-[#101828] text-slate-300 lg:flex">
        <div className="flex h-[76px] items-center gap-3 px-6">
          <div className="grid size-9 place-items-center rounded-xl bg-blue-500 text-white shadow-lg shadow-blue-950/30">
            <Shield className="size-[19px]" strokeWidth={2.2} />
          </div>
          <div>
            <div className="text-[14px] font-semibold tracking-tight text-white">Agentic</div>
            <div className="text-[10px] font-medium tracking-[0.16em] text-slate-500 uppercase">Log intelligence</div>
          </div>
        </div>

        <div className="px-4 pt-5 pb-2 text-[10px] font-semibold tracking-[0.15em] text-slate-500 uppercase">
          Workspace
        </div>
        <nav className="space-y-1 px-3" aria-label="Navigation principale">
          {navigation.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              onClick={() => setActiveView(id)}
              className={`flex h-10 w-full items-center gap-3 rounded-lg px-3 text-left text-[13px] font-medium transition ${
                activeView === id
                  ? "bg-blue-500/15 text-blue-300 ring-1 ring-blue-400/15"
                  : "text-slate-400 hover:bg-slate-800/70 hover:text-slate-100"
              }`}
            >
              <Icon className="size-[17px]" />
              {label}
              {id === "investigations" && detectionCount > 0 && (
                <span className="ml-auto grid size-5 place-items-center rounded-full bg-rose-500/15 text-[10px] text-rose-300">
                  {detectionCount}
                </span>
              )}
            </button>
          ))}
        </nav>

        <div className="mt-8 px-4 pb-2 text-[10px] font-semibold tracking-[0.15em] text-slate-500 uppercase">
          System
        </div>
        <div className="mx-3 rounded-xl border border-slate-800 bg-slate-900/50 p-3.5">
          <div className="flex items-center justify-between text-[12px] font-medium text-slate-200">
            <span className="flex items-center gap-2"><Activity className="size-3.5 text-emerald-400" /> API status</span>
            <span className={`size-2 rounded-full ${connected ? "bg-emerald-400" : "bg-rose-400"}`} />
          </div>
          <p className="mt-2 text-[11px] text-slate-500">{connected ? "Connected · localhost:5080" : "Waiting for API"}</p>
          <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-slate-800">
            <div className={`h-full rounded-full transition-all ${connected ? "w-full bg-emerald-400" : "w-1/4 bg-rose-400"}`} />
          </div>
        </div>

        <div className="mt-auto p-3">
          <div className="flex w-full items-center gap-3 rounded-lg p-2.5 text-left">
            <div className="grid size-8 place-items-center rounded-full bg-slate-700 text-xs font-semibold text-slate-100">TA</div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-xs font-medium text-slate-200">Security analyst</div>
              <div className="text-[10px] text-slate-500">Local workspace</div>
            </div>
          </div>
        </div>
      </aside>

      <div className="min-h-screen lg:pl-[248px]">
        <header className="sticky top-0 z-20 flex h-[68px] items-center justify-between border-b border-slate-200/80 bg-white/90 px-5 backdrop-blur-xl sm:px-8">
          <div className="flex items-center gap-3">
            <div className="grid size-8 place-items-center rounded-lg bg-blue-600 text-white lg:hidden"><Shield className="size-4" /></div>
            <div className="hidden text-xs text-slate-400 sm:block">Workspace <ChevronRight className="mx-1 inline size-3" /></div>
            <div className="text-[13px] font-semibold text-slate-800">{viewTitle}</div>
          </div>
          <div className="flex items-center gap-2 sm:gap-3">
            <div className="relative hidden w-[250px] md:block">
              <Search className="absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-slate-400" />
              <Input
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search events..."
                aria-label="Search events"
                className="h-9 border-slate-200 bg-slate-50 pr-9 pl-9 text-xs shadow-none focus-visible:bg-white"
              />
              {query && <button type="button" aria-label="Clear search" onClick={() => setQuery("")} className="absolute top-1/2 right-2 grid size-6 -translate-y-1/2 place-items-center rounded text-slate-400 hover:bg-slate-200/70 hover:text-slate-700"><X className="size-3.5" /></button>}
            </div>
            <Button variant="ghost" size="icon" className="relative text-slate-500" aria-label="Open investigations" onClick={() => setActiveView("investigations")}>
              <Bell className="size-[17px]" />
              {detectionCount > 0 && <span className="absolute top-1.5 right-1.5 size-1.5 rounded-full bg-rose-500 ring-2 ring-white" />}
            </Button>
            <Separator orientation="vertical" className="mx-1 hidden h-6 sm:block" />
            <Button variant="outline" className="h-9 gap-2 border-slate-200 bg-white px-3 text-xs shadow-sm" onClick={() => void refresh()} disabled={loading}>
              <RefreshCw className={`size-3.5 ${loading ? "animate-spin" : ""}`} />
              <span className="hidden sm:inline">Refresh</span>
            </Button>
          </div>
        </header>

        <div className="border-b border-slate-200 bg-white px-3 py-2 lg:hidden">
          <nav className="flex gap-1 overflow-x-auto">
            {navigation.map(({ id, label, icon: Icon }) => (
              <button key={id} onClick={() => setActiveView(id)} className={`flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-xs ${activeView === id ? "bg-blue-50 font-semibold text-blue-700" : "text-slate-500"}`}>
                <Icon className="size-3.5" />{label}
              </button>
            ))}
          </nav>
        </div>
        <div className="border-b border-slate-200 bg-white px-4 pb-3 md:hidden">
          <div className="relative">
            <Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-slate-400" />
            <Input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search events, users, IPs..." aria-label="Search events" className="h-10 border-slate-200 bg-slate-50 pr-10 pl-10 text-sm" />
            {query && <button type="button" aria-label="Clear search" onClick={() => setQuery("")} className="absolute top-1/2 right-2 grid size-7 -translate-y-1/2 place-items-center rounded text-slate-400 hover:bg-slate-200/70 hover:text-slate-700"><X className="size-4" /></button>}
          </div>
        </div>

        <main className="mx-auto max-w-[1500px] p-5 sm:p-8">
          <div className="mb-7 flex flex-col justify-between gap-4 md:flex-row md:items-end">
            <div>
              <div className="mb-2 flex items-center gap-2 text-[11px] font-medium text-slate-400">
                <span>Security operations</span><ChevronRight className="size-3" /><span className="text-slate-500">Live workspace</span>
              </div>
              <h1 className="text-[25px] font-semibold tracking-[-0.04em] text-slate-900">{viewTitle}</h1>
              <p className="mt-1 text-[13px] text-slate-500">
                {activeView === "overview" && "Monitor activity, detections and investigation workflows."}
                {activeView === "assistant" && "Ask questions in plain language; answers cite the events they rely on."}
                {activeView === "investigations" && "Evidence-led analysis from your deterministic agent pipeline."}
                {activeView === "events" && "Search and review normalized security events."}
                {activeView === "sources" && "Connected log sources and their latest activity."}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Dialog open={importOpen} onOpenChange={setImportOpen}>
                <DialogTrigger asChild>
                  <Button variant="outline" className="h-9 gap-2 border-slate-200 bg-white text-xs shadow-sm">
                    <Upload className="size-3.5" /> Import logs
                  </Button>
                </DialogTrigger>
                <DialogContent className="max-w-xl">
                  <DialogHeader>
                    <DialogTitle>Import log events</DialogTitle>
                    <DialogDescription>Import pipe-delimited or timestamp / level / component log lines. Invalid rows are reported after processing.</DialogDescription>
                  </DialogHeader>
                  <div className="space-y-4 py-2">
                    <label
                      onDragOver={(event) => { event.preventDefault(); setFileDropActive(true) }}
                      onDragLeave={(event) => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setFileDropActive(false) }}
                      onDrop={(event) => { event.preventDefault(); setFileDropActive(false); void importFile(event.dataTransfer.files[0]) }}
                      className={`relative flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed px-5 py-7 text-center transition ${fileDropActive ? "border-blue-500 bg-blue-50 ring-2 ring-blue-500/10" : "border-slate-300 bg-slate-50 hover:border-blue-400 hover:bg-blue-50/40"}`}
                    >
                      <Upload className="mb-2 size-5 text-slate-400" />
                      <span className="text-sm font-medium text-slate-700">{content ? "Choose another file" : "Choose a .log or .txt file"}</span>
                      <span className="mt-1 text-xs text-slate-400">or drop a file here</span>
                      <input type="file" accept=".log,.txt,text/plain" className="sr-only" onChange={(event) => { void importFile(event.target.files?.[0]); event.currentTarget.value = "" }} />
                    </label>
                    {content && <div className="flex items-center justify-between rounded-lg border border-blue-100 bg-blue-50/60 px-3 py-2 text-xs"><span className="truncate font-medium text-blue-800">{source} <span className="font-normal text-blue-600">· {content.split(/\r?\n/).filter((line) => line.trim()).length.toLocaleString("en-US")} lines</span></span><button type="button" onClick={() => { setContent(""); setSource("manual-import") }} className="ml-2 shrink-0 rounded px-2 py-1 text-blue-700 hover:bg-blue-100">Clear</button></div>}
                    <div className="grid gap-2">
                      <label htmlFor="source-name" className="text-xs font-medium text-slate-600">Source name</label>
                      <Input id="source-name" value={source} onChange={(event) => setSource(event.target.value)} placeholder="manual-import" />
                    </div>
                    <div className="grid gap-2">
                      <label htmlFor="log-content" className="text-xs font-medium text-slate-600">Or paste log lines</label>
                      <textarea id="log-content" value={content} onChange={(event) => setContent(event.target.value)} rows={7} placeholder="2026-09-25T17:00:30Z WARN [security] Failed authentication attempt username=admin source=10.10.20.15" className="resize-y rounded-lg border border-slate-200 bg-white px-3 py-2 font-mono text-xs leading-5 outline-none placeholder:text-slate-400 focus-visible:ring-2 focus-visible:ring-blue-500/20" />
                    </div>
                  </div>
                  <DialogFooter>
                    <Button variant="outline" onClick={() => setImportOpen(false)}>Cancel</Button>
                    <Button onClick={() => void submitImport()} disabled={importing} className="gap-2">
                      {importing ? <LoaderCircle className="size-4 animate-spin" /> : <Upload className="size-4" />}
                      {importing ? "Importing..." : "Import events"}
                    </Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
              <Button onClick={() => void startInvestigation()} disabled={investigating} className="h-9 gap-2 bg-blue-600 px-3.5 text-xs text-white shadow-sm shadow-blue-900/15 hover:bg-blue-700">
                {investigating ? <LoaderCircle className="size-3.5 animate-spin" /> : <Sparkles className="size-3.5" />}
                {investigating ? "Investigating..." : "New investigation"}
              </Button>
            </div>
          </div>

          {!connected && !loading && (
            <div className="mb-6 flex items-center gap-3 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <AlertTriangle className="size-4 shrink-0" />
              <span>API unreachable. Start the Agentic Log Analyzer API on <code className="font-mono">localhost:5080</code>, then refresh.</span>
            </div>
          )}

          {activeView === "overview" && (
            <OverviewView
              events={events}
              filteredEvents={visibleEvents}
              report={report}
              activityData={activityData}
              categoryData={categoryData}
              loading={loading}
              failureCount={failureCount}
              sourceCount={sources.length}
              onInvestigate={() => void startInvestigation()}
              onShowEvents={() => setActiveView("events")}
              onShowInvestigations={() => setActiveView("investigations")}
            />
          )}
          <div className={activeView === "assistant" ? undefined : "hidden"}><LogChat /></div>
          {activeView === "investigations" && <InvestigationView report={report} loading={loading || investigating} />}
          {activeView === "events" && <EventsView events={visibleEvents} loading={loading} query={query} />}
          {activeView === "sources" && <SourcesView sources={sources} loading={loading} />}

          <footer className="mt-10 flex flex-col justify-between gap-2 border-t border-slate-200 pt-5 text-[11px] text-slate-400 sm:flex-row">
            <span>Agentic Log Analyzer <span className="mx-1.5">·</span> Deterministic engine</span>
            <span className="flex items-center gap-2"><span className={`size-1.5 rounded-full ${connected ? "bg-emerald-500" : "bg-rose-500"}`} />{connected ? "All systems operational" : "API connection required"}<span className="mx-1.5">·</span>Local JSONL storage</span>
          </footer>
        </main>
      </div>
    </div>
  )
}

function OverviewView({
  events,
  filteredEvents,
  report,
  activityData,
  categoryData,
  loading,
  failureCount,
  sourceCount,
  onInvestigate,
  onShowEvents,
  onShowInvestigations,
}: {
  events: CanonicalEvent[]
  filteredEvents: CanonicalEvent[]
  report: InvestigationReport | null
  activityData: { label: string; events: number; failures: number; order: number }[]
  categoryData: { name: string; count: number }[]
  loading: boolean
  failureCount: number
  sourceCount: number
  onInvestigate: () => void
  onShowEvents: () => void
  onShowInvestigations: () => void
}) {
  const stats = [
    { label: "Events indexed", value: events.length.toLocaleString("en-US"), note: "Across all log sources", icon: Database, tone: "blue" },
    { label: "Detections", value: String(report?.detections.length ?? 0), note: report?.detections.length ? "Require analyst review" : "No active detections", icon: ShieldAlert, tone: "rose" },
    { label: "Failed events", value: String(failureCount), note: "Normalized failures", icon: AlertTriangle, tone: "amber" },
    { label: "Log sources", value: String(sourceCount), note: "Distinct source names", icon: Network, tone: "violet" },
  ]
  const toneClass: Record<string, string> = {
    blue: "bg-blue-50 text-blue-600",
    rose: "bg-rose-50 text-rose-600",
    amber: "bg-amber-50 text-amber-600",
    violet: "bg-violet-50 text-violet-600",
  }

  return (
    <div className="space-y-6">
      <section className="relative overflow-hidden rounded-2xl bg-[#132342] px-6 py-6 text-white shadow-sm sm:px-8 sm:py-7">
        <div className="absolute -top-24 right-14 size-64 rounded-full border border-white/5" />
        <div className="absolute -top-10 right-24 size-44 rounded-full border border-white/5" />
        <div className="absolute right-0 bottom-0 h-40 w-1/2 bg-gradient-to-l from-blue-500/15 to-transparent" />
        <div className="relative z-10 flex flex-col justify-between gap-5 md:flex-row md:items-center">
          <div className="max-w-2xl">
            <div className="mb-3 inline-flex items-center gap-2 rounded-full border border-blue-300/20 bg-blue-400/10 px-2.5 py-1 text-[10px] font-medium tracking-wide text-blue-200">
              <Sparkles className="size-3" /> DETERMINISTIC INVESTIGATION PIPELINE
            </div>
            <h2 className="text-xl font-semibold tracking-tight sm:text-[22px]">Find the signal in your logs.</h2>
            <p className="mt-1.5 max-w-xl text-[12px] leading-5 text-slate-300 sm:text-[13px]">
              Four specialized agents search, detect, correlate and report with evidence linked to your events.
            </p>
          </div>
          <Button onClick={onInvestigate} className="relative z-10 h-10 shrink-0 gap-2 bg-white px-4 text-xs font-semibold text-blue-900 hover:bg-blue-50">
            <Sparkles className="size-3.5" /> Start investigation <ArrowRight className="size-3.5" />
          </Button>
        </div>
      </section>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map(({ label, value, note, icon: Icon, tone }) => (
          <Card key={label} className="gap-0 rounded-xl border-slate-200/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
            <CardContent className="p-4 sm:p-5">
              <div className="flex items-start justify-between">
                <div>
                  <div className="text-[11px] font-medium text-slate-500">{label}</div>
                  <div className="mt-2 text-[26px] leading-none font-semibold tracking-[-0.04em] text-slate-900">{loading ? "—" : value}</div>
                </div>
                <div className={`grid size-9 place-items-center rounded-lg ${toneClass[tone]}`}><Icon className="size-[17px]" /></div>
              </div>
              <div className="mt-3 flex items-center gap-1.5 text-[10px] text-slate-400"><span className="text-slate-500">{note}</span></div>
            </CardContent>
          </Card>
        ))}
      </section>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1.65fr)_minmax(300px,.8fr)]">
        <Card className="rounded-xl border-slate-200/80 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="flex-row items-start justify-between space-y-0 px-5 pt-5 pb-1">
            <div>
              <CardTitle className="text-[13px] font-semibold text-slate-800">Event activity</CardTitle>
              <CardDescription className="mt-1 text-[11px]">Ingested events over time</CardDescription>
            </div>
            <div className="flex items-center gap-3 text-[10px] text-slate-500">
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-blue-500" />Events</span>
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-rose-400" />Failures</span>
            </div>
          </CardHeader>
          <CardContent className="px-2 pt-4 pb-4 sm:px-4">
            {activityData.length ? (
              <ResponsiveContainer width="100%" height={248}>
                <AreaChart data={activityData} margin={{ top: 8, right: 12, left: -16, bottom: 0 }}>
                  <defs>
                    <linearGradient id="eventFill" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#3b82f6" stopOpacity={0.2} /><stop offset="95%" stopColor="#3b82f6" stopOpacity={0} /></linearGradient>
                  </defs>
                  <CartesianGrid stroke="#eef1f5" strokeDasharray="3 5" vertical={false} />
                  <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#94a3b8", fontSize: 10 }} minTickGap={24} />
                  <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: "#94a3b8", fontSize: 10 }} />
                  <Tooltip contentStyle={{ borderRadius: 10, borderColor: "#e2e8f0", fontSize: 11, boxShadow: "0 8px 24px #0f172a12" }} />
                  <Area type="monotone" dataKey="events" name="Events" stroke="#3b82f6" strokeWidth={2} fill="url(#eventFill)" activeDot={{ r: 4, fill: "#2563eb" }} />
                  <Area type="monotone" dataKey="failures" name="Failures" stroke="#fb7185" strokeWidth={1.6} fill="transparent" activeDot={{ r: 3, fill: "#f43f5e" }} />
                </AreaChart>
              </ResponsiveContainer>
            ) : (
              <ChartEmpty loading={loading} message="Import logs to see event activity." />
            )}
          </CardContent>
        </Card>

        <Card className="rounded-xl border-slate-200/80 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="px-5 pt-5 pb-1">
            <CardTitle className="text-[13px] font-semibold text-slate-800">Events by category</CardTitle>
            <CardDescription className="mt-1 text-[11px]">Top categories in your dataset</CardDescription>
          </CardHeader>
          <CardContent className="px-2 pt-4 pb-5">
            {categoryData.length ? (
              <ResponsiveContainer width="100%" height={248}>
                <BarChart data={categoryData} layout="vertical" margin={{ top: 4, right: 18, left: 12, bottom: 0 }}>
                  <CartesianGrid stroke="#eef1f5" strokeDasharray="3 5" horizontal={false} />
                  <XAxis type="number" allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: "#94a3b8", fontSize: 10 }} />
                  <YAxis type="category" dataKey="name" width={80} tickLine={false} axisLine={false} tick={{ fill: "#64748b", fontSize: 10 }} />
                  <Tooltip cursor={{ fill: "#f8fafc" }} contentStyle={{ borderRadius: 10, borderColor: "#e2e8f0", fontSize: 11 }} />
                  <Bar dataKey="count" name="Events" fill="#5b8def" radius={[0, 5, 5, 0]} barSize={16} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <ChartEmpty loading={loading} message="Category distribution appears after ingestion." />
            )}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1.65fr)_minmax(300px,.8fr)]">
        <Card className="gap-0 overflow-hidden rounded-xl border-slate-200/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="flex-row items-center justify-between space-y-0 border-b border-slate-100 px-5 py-4">
            <div>
              <CardTitle className="text-[13px] font-semibold text-slate-800">Recent events</CardTitle>
              <CardDescription className="mt-1 text-[11px]">Latest normalized activity</CardDescription>
            </div>
            <Button variant="ghost" size="sm" onClick={onShowEvents} className="h-8 gap-1 px-2 text-[11px] text-blue-700">View all <ArrowRight className="size-3" /></Button>
          </CardHeader>
          <EventTable events={filteredEvents.slice(0, 6)} loading={loading} compact />
        </Card>

        <Card className="gap-0 rounded-xl border-slate-200/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="flex-row items-center justify-between space-y-0 px-5 pt-5 pb-3">
            <div>
              <CardTitle className="text-[13px] font-semibold text-slate-800">Detection queue</CardTitle>
              <CardDescription className="mt-1 text-[11px]">Rule-based signals for review</CardDescription>
            </div>
            <Badge variant="secondary" className="bg-slate-100 text-[10px] text-slate-600">{report?.detections.length ?? 0} total</Badge>
          </CardHeader>
          <CardContent className="space-y-3 px-5 pt-1 pb-5">
            {report?.detections.length ? report.detections.slice(0, 3).map((detection) => (
              <button key={detection.ruleId + detection.evidenceEventIds[0]} onClick={onShowInvestigations} className="w-full rounded-lg border border-rose-100 bg-rose-50/50 p-3 text-left transition hover:border-rose-200 hover:bg-rose-50">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex gap-2.5">
                    <div className="mt-0.5 grid size-7 shrink-0 place-items-center rounded-md bg-rose-100 text-rose-600"><ShieldAlert className="size-3.5" /></div>
                    <div><div className="text-[11px] font-semibold text-slate-800">{detection.title}</div><div className="mt-1 line-clamp-2 text-[10px] leading-4 text-slate-500">{detection.description}</div></div>
                  </div>
                  <Badge variant="destructive" className="h-5 px-1.5 text-[9px] uppercase">{detection.severity}</Badge>
                </div>
                <div className="mt-2.5 flex items-center justify-between pl-9 text-[9px] text-slate-400"><span>{detection.ruleId} · {detection.evidenceEventIds.length} evidence events</span><span>{formatShortTime(detection.lastSeen)}</span></div>
              </button>
            )) : (
              <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed border-slate-200 bg-slate-50/60 px-5 text-center">
                <Shield className="mb-2 size-5 text-slate-300" />
                <p className="text-[11px] font-medium text-slate-600">No active detections</p>
                <p className="mt-1 text-[10px] text-slate-400">Deterministic rules are checked as events arrive.</p>
              </div>
            )}
            <button onClick={onShowInvestigations} className="flex w-full items-center justify-between border-t border-slate-100 pt-3 text-[10px] font-medium text-slate-500 hover:text-blue-700">
              Open investigation workspace <ArrowRight className="size-3" />
            </button>
          </CardContent>
        </Card>
      </section>
    </div>
  )
}

function ChartEmpty({ loading, message }: { loading: boolean; message: string }) {
  return <div className="grid h-[248px] place-items-center text-center text-xs text-slate-400">{loading ? "Loading data…" : message}</div>
}

function EventTable({ events, loading, compact = false }: { events: CanonicalEvent[]; loading: boolean; compact?: boolean }) {
  if (loading && !events.length) return <div className="p-8 text-center text-xs text-slate-400">Loading events…</div>
  if (!events.length) return <div className="p-10 text-center"><FileSearch className="mx-auto mb-2 size-5 text-slate-300" /><div className="text-xs font-medium text-slate-600">No events found</div><div className="mt-1 text-[10px] text-slate-400">Try a different search or import a log file.</div></div>
  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="border-slate-100 hover:bg-transparent">
            <TableHead className="h-9 pl-5 text-[10px] font-semibold tracking-wide text-slate-400 uppercase">Event</TableHead>
            <TableHead className="h-9 text-[10px] font-semibold tracking-wide text-slate-400 uppercase">Outcome</TableHead>
            <TableHead className="h-9 text-[10px] font-semibold tracking-wide text-slate-400 uppercase">Identity</TableHead>
            {!compact && <TableHead className="h-9 text-[10px] font-semibold tracking-wide text-slate-400 uppercase">Source</TableHead>}
            <TableHead className="h-9 pr-5 text-right text-[10px] font-semibold tracking-wide text-slate-400 uppercase">Time</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {events.map((event) => (
            <TableRow key={event.id} className="border-slate-100 hover:bg-slate-50/80">
              <TableCell className="max-w-[300px] pl-5 py-3">
                <div className="flex items-center gap-2.5">
                  <div className={`grid size-7 shrink-0 place-items-center rounded-md ${resultTone(event.result) === "danger" ? "bg-rose-50 text-rose-600" : "bg-blue-50 text-blue-600"}`}>
                    {event.category.toLowerCase().includes("auth") || event.category.toLowerCase().includes("security") ? <Fingerprint className="size-3.5" /> : <Activity className="size-3.5" />}
                  </div>
                  <div className="min-w-0"><div className="truncate text-[11px] font-medium text-slate-800">{event.category} <span className="font-normal text-slate-400">/</span> {event.action}</div><div className="truncate text-[10px] text-slate-400">{event.rawContent}</div></div>
                </div>
              </TableCell>
              <TableCell><ResultBadge result={event.result} /></TableCell>
              <TableCell><div className="flex items-center gap-1.5 text-[11px] text-slate-600"><UserRound className="size-3 text-slate-400" />{event.user ?? "—"}</div><div className="mt-0.5 text-[10px] text-slate-400">{event.sourceIp ?? event.device ?? "No network identity"}</div></TableCell>
              {!compact && <TableCell><div className="text-[11px] text-slate-600">{event.sourceName}</div><div className="text-[10px] text-slate-400">{event.sourceType}</div></TableCell>}
              <TableCell className="pr-5 text-right"><div className="whitespace-nowrap text-[10px] text-slate-500">{formatTimestamp(event.timestamp)}</div></TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function InvestigationView({ report, loading }: { report: InvestigationReport | null; loading: boolean }) {
  if (!report && loading) return <div className="grid min-h-80 place-items-center"><LoaderCircle className="size-6 animate-spin text-blue-500" /></div>
  if (!report) return <ChartEmpty loading={false} message="Start an investigation to generate an evidence-backed report." />

  return (
    <div className="space-y-5">
      <Card className="overflow-hidden rounded-xl border-blue-100 bg-gradient-to-br from-white to-blue-50/70 shadow-[0_2px_10px_rgba(37,99,235,.04)]">
        <CardContent className="flex flex-col justify-between gap-5 p-5 sm:flex-row sm:items-center sm:p-6">
          <div>
            <div className="mb-2 flex items-center gap-2"><Badge className="bg-blue-600 text-[9px] tracking-wide text-white">COMPLETED</Badge><span className="text-[10px] text-slate-400">{formatTimestamp(report.createdAt)}</span></div>
            <h2 className="text-lg font-semibold tracking-tight text-slate-900">Investigation report</h2>
            <p className="mt-1 text-xs text-slate-500">{report.summary}</p>
          </div>
          <div className="flex gap-5 sm:gap-7">
            <MiniMetric label="Events reviewed" value={report.events.length} />
            <MiniMetric label="Detections" value={report.detections.length} highlight={report.detections.length > 0} />
            <MiniMetric label="Correlations" value={report.correlations.length} />
          </div>
        </CardContent>
      </Card>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,.8fr)]">
        <Card className="gap-0 overflow-hidden rounded-xl border-slate-200/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="border-b border-slate-100 px-5 py-4">
            <div className="flex items-center gap-2"><ShieldAlert className="size-4 text-rose-500" /><CardTitle className="text-[13px] font-semibold">Deterministic detections</CardTitle></div>
            <CardDescription className="text-[11px]">Every finding includes the event IDs used as evidence.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-4">
            {report.detections.length ? report.detections.map((detection) => (
              <div key={detection.ruleId + detection.evidenceEventIds[0]} className="rounded-xl border border-rose-100 bg-rose-50/40 p-4">
                <div className="flex flex-wrap items-center gap-2"><Badge variant="destructive" className="text-[9px] uppercase">{detection.severity}</Badge><span className="font-mono text-[10px] text-slate-400">{detection.ruleId}</span><span className="ml-auto text-[10px] text-slate-400">{formatTimestamp(detection.firstSeen)} – {formatShortTime(detection.lastSeen)}</span></div>
                <h3 className="mt-2 text-[13px] font-semibold text-slate-800">{detection.title}</h3>
                <p className="mt-1 text-[11px] leading-5 text-slate-600">{detection.description}</p>
                <div className="mt-3 flex flex-wrap gap-2">{detection.user && <Badge variant="outline" className="bg-white text-[10px]"><UserRound className="mr-1 size-3" />{detection.user}</Badge>}{detection.sourceIp && <Badge variant="outline" className="bg-white font-mono text-[10px]"><Globe2 className="mr-1 size-3" />{detection.sourceIp}</Badge>}<Badge variant="outline" className="bg-white text-[10px]">{detection.evidenceEventIds.length} evidence events</Badge></div>
                <details className="mt-3 border-t border-rose-100 pt-2"><summary className="cursor-pointer text-[10px] font-medium text-rose-700">View evidence IDs</summary><div className="mt-2 space-y-1">{detection.evidenceEventIds.map((id) => <div key={id} className="font-mono text-[9px] text-slate-500">{id}</div>)}</div></details>
              </div>
            )) : <EmptyPanel icon={Shield} title="No rule matched" detail="No configured deterministic rule matched the selected events." />}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card className="rounded-xl border-slate-200/80 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
            <CardHeader className="px-5 pt-5 pb-2"><div className="flex items-center gap-2"><Network className="size-4 text-violet-500" /><CardTitle className="text-[13px] font-semibold">Correlated entities</CardTitle></div><CardDescription className="text-[11px]">Shared identities and infrastructure in this result set.</CardDescription></CardHeader>
            <CardContent className="space-y-2 px-5 pb-5">
              {report.correlations.length ? report.correlations.slice(0, 8).map((correlation) => {
                const Icon = correlation.entityType === "user" ? UserRound : correlation.entityType === "sourceIp" ? Globe2 : Server
                return <div key={`${correlation.entityType}-${correlation.entityValue}`} className="flex items-center gap-3 rounded-lg border border-slate-100 bg-slate-50/70 p-2.5"><div className="grid size-8 place-items-center rounded-md bg-white text-slate-500 shadow-sm"><Icon className="size-3.5" /></div><div className="min-w-0 flex-1"><div className="truncate text-[11px] font-medium text-slate-700">{correlation.entityValue}</div><div className="text-[9px] text-slate-400">Shared {correlation.entityType} · {correlation.eventIds.length} events</div></div><ChevronRight className="size-3.5 text-slate-300" /></div>
              }) : <p className="py-5 text-center text-[11px] text-slate-400">No repeated entities among these events.</p>}
            </CardContent>
          </Card>

          <Card className="rounded-xl border-slate-200/80 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
            <CardHeader className="px-5 pt-5 pb-2"><div className="flex items-center gap-2"><Sparkles className="size-4 text-amber-500" /><CardTitle className="text-[13px] font-semibold">Assessment</CardTitle></div><CardDescription className="text-[11px]">Facts and hypotheses are kept separate.</CardDescription></CardHeader>
            <CardContent className="space-y-4 px-5 pb-5">
              <div><div className="mb-2 text-[9px] font-semibold tracking-wider text-emerald-700 uppercase">Observed facts</div>{report.facts.length ? report.facts.map((fact, index) => <div key={index} className="mb-2 rounded-md border-l-2 border-emerald-400 bg-emerald-50/50 px-3 py-2 text-[10px] leading-4 text-slate-600">{fact.statement}<div className="mt-1 text-[9px] text-slate-400">Evidence: {fact.evidenceEventIds.length} event ID(s)</div></div>) : <p className="text-[10px] text-slate-400">No detection facts for this query.</p>}</div>
              <div><div className="mb-2 text-[9px] font-semibold tracking-wider text-amber-700 uppercase">Hypotheses to verify</div>{report.hypotheses.length ? report.hypotheses.map((item, index) => <div key={index} className="rounded-md border-l-2 border-amber-400 bg-amber-50/60 px-3 py-2 text-[10px] leading-4 text-slate-600">{item.statement}</div>) : <p className="text-[10px] text-slate-400">No hypotheses generated.</p>}</div>
              {report.recommendations.length > 0 && <div><div className="mb-2 text-[9px] font-semibold tracking-wider text-blue-700 uppercase">Recommended next steps</div><ul className="space-y-1.5">{report.recommendations.map((item, index) => <li key={index} className="flex gap-2 text-[10px] leading-4 text-slate-600"><ArrowRight className="mt-0.5 size-3 shrink-0 text-blue-500" />{item}</li>)}</ul></div>}
            </CardContent>
          </Card>
        </div>
      </section>

      <Card className="rounded-xl border-slate-200/80 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
        <CardHeader className="px-5 pt-5 pb-3"><div className="flex items-center gap-2"><WorkflowIcon /><CardTitle className="text-[13px] font-semibold">Orchestrator trace</CardTitle></div><CardDescription className="text-[11px]">Fixed sequence · deterministic tools · no LLM call</CardDescription></CardHeader>
        <CardContent className="grid gap-3 px-5 pb-5 sm:grid-cols-2 xl:grid-cols-4">
          {report.agentTrace.map((step, index) => (
            <div key={step.agent} className="relative flex items-center gap-3 rounded-lg border border-slate-100 bg-slate-50/70 p-3">
              {index < report.agentTrace.length - 1 && <div className="absolute top-1/2 -right-3 z-10 hidden h-px w-3 bg-slate-300 xl:block" />}
              <div className="grid size-8 shrink-0 place-items-center rounded-lg bg-white text-blue-600 shadow-sm">{index === 0 ? <FileSearch className="size-4" /> : index === 1 ? <ShieldAlert className="size-4" /> : index === 2 ? <Network className="size-4" /> : <FileClock className="size-4" />}</div>
              <div className="min-w-0"><div className="truncate text-[10px] font-semibold text-slate-700">{step.agent}</div><div className="mt-0.5 flex items-center gap-1.5 text-[9px] text-slate-400"><Check className="size-3 text-emerald-500" />{step.outcome} · {step.evidenceCount} result(s)</div></div>
            </div>
          ))}
        </CardContent>
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2 border-t border-slate-100 px-5 py-3 text-[9px] text-slate-400"><span>Investigation ID: <code className="font-mono text-slate-500">{report.id}</code></span><span>LLM used: <strong className="font-medium text-slate-600">{report.llmUsed ? "yes" : "no"}</strong></span><span>Events analyzed: <strong className="font-medium text-slate-600">{report.events.length}</strong></span></div>
      </Card>
    </div>
  )
}

function WorkflowIcon() {
  return <Layers3 className="size-4 text-blue-600" />
}

function MiniMetric({ label, value, highlight = false }: { label: string; value: number; highlight?: boolean }) {
  return <div className="min-w-[70px]"><div className="text-[9px] font-medium tracking-wide text-slate-400 uppercase">{label}</div><div className={`mt-1 text-[20px] font-semibold tracking-tight ${highlight ? "text-rose-600" : "text-slate-800"}`}>{value}</div></div>
}

function EmptyPanel({ icon: Icon, title, detail }: { icon: typeof Shield; title: string; detail: string }) {
  return <div className="grid min-h-32 place-items-center rounded-lg border border-dashed border-slate-200 bg-slate-50/60 px-5 text-center"><div><Icon className="mx-auto mb-2 size-5 text-slate-300" /><p className="text-[11px] font-medium text-slate-600">{title}</p><p className="mt-1 text-[10px] text-slate-400">{detail}</p></div></div>
}

function EventsView({ events, loading, query }: { events: CanonicalEvent[]; loading: boolean; query: string }) {
  const [page, setPage] = useState(1)
  const pageSize = 25
  const pageCount = Math.max(1, Math.ceil(events.length / pageSize))
  const currentPage = Math.min(page, pageCount)
  const pageEvents = events.slice((currentPage - 1) * pageSize, currentPage * pageSize)

  return (
    <Card className="gap-0 overflow-hidden rounded-xl border-slate-200/80 py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
      <CardHeader className="flex-row items-center justify-between space-y-0 border-b border-slate-100 px-5 py-4">
        <div><CardTitle className="text-[13px] font-semibold">Normalized event stream</CardTitle><CardDescription className="mt-1 text-[11px]">{events.length.toLocaleString("en-US")} events{query ? ` matching “${query}”` : " across all sources"}</CardDescription></div>
        <Badge variant="outline" className="gap-1.5 bg-slate-50 text-[10px] text-slate-600"><span className="size-1.5 rounded-full bg-slate-400" /> Stored events</Badge>
      </CardHeader>
      <EventTable events={pageEvents} loading={loading} />
      {!loading && events.length > pageSize && <div className="flex flex-col gap-3 border-t border-slate-100 px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-5">
        <span className="text-[11px] text-slate-500">Showing {((currentPage - 1) * pageSize + 1).toLocaleString("en-US")}–{Math.min(currentPage * pageSize, events.length).toLocaleString("en-US")} of {events.length.toLocaleString("en-US")}</span>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => setPage(Math.max(1, currentPage - 1))} disabled={currentPage === 1} className="h-8 gap-1 text-xs"><ChevronLeft className="size-3.5" />Previous</Button>
          <span className="min-w-16 text-center text-[11px] tabular-nums text-slate-500">Page {currentPage} of {pageCount}</span>
          <Button variant="outline" size="sm" onClick={() => setPage(Math.min(pageCount, currentPage + 1))} disabled={currentPage === pageCount} className="h-8 gap-1 text-xs">Next<ChevronRight className="size-3.5" /></Button>
        </div>
      </div>}
    </Card>
  )
}

function SourcesView({ sources, loading }: { sources: { name: string; events: number; types: Set<string>; latest: string }[]; loading: boolean }) {
  if (loading) return <ChartEmpty loading message="Loading sources…" />
  if (!sources.length) return <EmptyPanel icon={Database} title="No log sources yet" detail="Import a log file to register its source." />
  return <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{sources.map((source) => <Card key={source.name} className="rounded-xl border-slate-200/80 shadow-[0_1px_2px_rgba(15,23,42,.03)]"><CardContent className="p-5"><div className="flex items-start justify-between"><div className="grid size-10 place-items-center rounded-xl bg-blue-50 text-blue-600"><Server className="size-5" /></div><Badge variant="outline" className="border-blue-200 bg-blue-50 text-[9px] text-blue-700">IMPORTED</Badge></div><h3 className="mt-4 truncate text-sm font-semibold text-slate-800">{source.name}</h3><p className="mt-1 text-[10px] text-slate-400">{[...source.types].join(", ")} connector</p><Separator className="my-4" /><div className="flex justify-between"><div><div className="text-[9px] tracking-wide text-slate-400 uppercase">Events</div><div className="mt-1 text-lg font-semibold text-slate-800">{source.events.toLocaleString("en-US")}</div></div><div className="text-right"><div className="text-[9px] tracking-wide text-slate-400 uppercase">Last event</div><div className="mt-2 text-[10px] text-slate-600">{formatTimestamp(source.latest)}</div></div></div></CardContent></Card>)}</div>
}

export default App
