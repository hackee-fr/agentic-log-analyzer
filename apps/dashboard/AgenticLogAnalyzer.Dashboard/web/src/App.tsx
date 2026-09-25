import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent as ReactKeyboardEvent } from "react"
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
  FileText,
  Globe2,
  Monitor,
  Layers3,
  LoaderCircle,
  Network,
  RefreshCw,
  Search,
  Server,
  Settings2,
  Shield,
  ShieldAlert,
  Sparkles,
  Trash2,
  Upload,
  UserRound,
  X,
} from "lucide-react"
import {
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
  deleteEvents,
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
  CardAction,
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
import { Skeleton } from "@/components/ui/skeleton"
import { EventDetailDialog } from "@/components/event-detail-dialog"
import { buildActivity, describeStep, type ActivityBucket } from "@/lib/activity"
import { LogChat } from "@/components/log-chat"
import { SettingsView } from "@/components/settings-view"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"

type View = "overview" | "assistant" | "investigations" | "events" | "sources" | "settings"

const navigation: { id: View; label: string; icon: typeof Activity }[] = [
  { id: "overview", label: "Overview", icon: Layers3 },
  { id: "assistant", label: "Assistant", icon: Bot },
  { id: "investigations", label: "Investigations", icon: ShieldAlert },
  { id: "events", label: "Events", icon: FileSearch },
  { id: "sources", label: "Sources", icon: Database },
  { id: "settings", label: "Settings", icon: Settings2 },
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

type ResultFilter = "all" | "failure" | "warning" | "success"

const resultFilters: { id: ResultFilter; label: string }[] = [
  { id: "all", label: "All" },
  { id: "failure", label: "Failures" },
  { id: "warning", label: "Warnings" },
  { id: "success", label: "Success" },
]

const matchesResultFilter = (event: CanonicalEvent, filter: ResultFilter) => {
  if (filter === "all") return true
  const tone = resultTone(event.result)
  return filter === "failure" ? tone === "danger" : filter === "warning" ? tone === "warning" : tone === "success"
}

const readViewFromHash = (): View => {
  const hash = window.location.hash.slice(1)
  return navigation.some((item) => item.id === hash) ? (hash as View) : "overview"
}

const isTypingTarget = (target: EventTarget | null) =>
  target instanceof HTMLElement && (target.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(target.tagName))

const fetchDashboardData = async () =>
  Promise.all([getEvents(), runInvestigation("", 1000)])

function ResultBadge({ result }: { result: string | null }) {
  const tone = resultTone(result)
  const colors = {
    danger: "border-rose-500/30 bg-rose-500/10 text-rose-400",
    warning: "border-amber-500/30 bg-amber-500/10 text-amber-400",
    success: "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",
    neutral: "border-border bg-muted/40 text-foreground/70",
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
                : "bg-muted-foreground"
        }`}
      />
      {result || "event"}
    </Badge>
  )
}

function App() {
  const [events, setEvents] = useState<CanonicalEvent[]>([])
  const [report, setReport] = useState<InvestigationReport | null>(null)
  const [activeView, setActiveViewState] = useState<View>(readViewFromHash)
  const [query, setQuery] = useState("")
  const [resultFilter, setResultFilter] = useState<ResultFilter>("all")
  const [selectedEvent, setSelectedEvent] = useState<CanonicalEvent | null>(null)
  const searchInput = useRef<HTMLInputElement>(null)
  const mobileSearchInput = useRef<HTMLInputElement>(null)
  const mobileNav = useRef<HTMLElement>(null)
  const [connected, setConnected] = useState(false)
  const [loading, setLoading] = useState(true)
  const [investigating, setInvestigating] = useState(false)
  const [importing, setImporting] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [fileDropActive, setFileDropActive] = useState(false)
  const [content, setContent] = useState("")
  const [source, setSource] = useState("manual-import")

  // The active view lives in the URL hash so reloads, links and the back button keep it.
  const setActiveView = useCallback((view: View) => {
    setActiveViewState(view)
    if (window.location.hash !== `#${view}`) window.history.pushState(null, "", `#${view}`)
    window.scrollTo({ top: 0 })
  }, [])

  useEffect(() => {
    const onPopState = () => setActiveViewState(readViewFromHash())
    window.addEventListener("popstate", onPopState)
    return () => window.removeEventListener("popstate", onPopState)
  }, [])

  // Keep the active tab visible in the horizontally scrolling mobile navigation.
  useEffect(() => {
    mobileNav.current?.querySelector('[aria-current="page"]')?.scrollIntoView({ block: "nearest", inline: "center" })
  }, [activeView])

  // "/" focuses the search box, like most log and code search tools.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "/" || event.metaKey || event.ctrlKey || event.altKey || isTypingTarget(event.target)) return
      const input = searchInput.current?.offsetParent ? searchInput.current : mobileSearchInput.current
      if (!input) return
      event.preventDefault()
      input.focus()
      input.select()
    }
    window.addEventListener("keydown", onKeyDown)
    return () => window.removeEventListener("keydown", onKeyDown)
  }, [])

  const onSearchKeyDown = (event: ReactKeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Escape") {
      setQuery("")
      event.currentTarget.blur()
    }
    if (event.key === "Enter" && activeView !== "events") setActiveView("events")
  }

  const showEvents = (filter: ResultFilter = "all", nextQuery?: string) => {
    setResultFilter(filter)
    if (nextQuery !== undefined) setQuery(nextQuery)
    setActiveView("events")
  }

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

  const searchedEvents = useMemo(() => {
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

  const visibleEvents = useMemo(
    () => searchedEvents.filter((event) => matchesResultFilter(event, resultFilter)),
    [searchedEvents, resultFilter],
  )

  const resultCounts = useMemo(() => {
    const counts: Record<ResultFilter, number> = { all: searchedEvents.length, failure: 0, warning: 0, success: 0 }
    for (const event of searchedEvents) {
      if (matchesResultFilter(event, "failure")) counts.failure += 1
      else if (matchesResultFilter(event, "warning")) counts.warning += 1
      else if (matchesResultFilter(event, "success")) counts.success += 1
    }
    return counts
  }, [searchedEvents])

  const activity = useMemo(() => buildActivity(events), [events])

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

  const removeEvents = async (target: string | null) => {
    try {
      const result = await deleteEvents(target)
      await refresh()
      toast.success(`${result.deleted} event(s) deleted`, {
        description: target === null ? "All stored events were removed." : `Source: ${target}`,
      })
    } catch (error) {
      toast.error("Deletion failed", {
        description: error instanceof Error ? error.message : "Check that the API is running.",
      })
    }
  }

  const viewTitle = {
    overview: "Overview",
    assistant: "Log assistant",
    investigations: "Investigations",
    events: "Events",
    sources: "Log sources",
    settings: "Settings",
  }[activeView]

  return (
    <div className="min-h-screen bg-background text-foreground">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-[248px] flex-col border-r border-border bg-background text-muted-foreground/60 lg:flex">
        <div className="flex h-[76px] items-center gap-3 px-6">
          <div className="grid size-9 place-items-center rounded-xl bg-primary text-primary-foreground">
            <Shield className="size-[19px]" strokeWidth={2.2} />
          </div>
          <div>
            <div className="text-[14px] font-semibold tracking-tight text-foreground">Agentic</div>
            <div className="text-[10px] font-medium tracking-[0.16em] text-muted-foreground uppercase">Log intelligence</div>
          </div>
        </div>

        <div className="px-4 pt-5 pb-2 text-[10px] font-semibold tracking-[0.15em] text-muted-foreground uppercase">
          Workspace
        </div>
        <nav className="space-y-1 px-3" aria-label="Navigation principale">
          {navigation.map(({ id, label, icon: Icon }) => (
            <button
              key={id}
              type="button"
              onClick={() => setActiveView(id)}
              aria-current={activeView === id ? "page" : undefined}
              className={`flex h-10 w-full items-center gap-3 rounded-lg px-3 text-left text-[13px] font-medium transition focus-visible:ring-2 focus-visible:ring-blue-400/60 focus-visible:outline-none ${
                activeView === id
                  ? "bg-muted text-foreground"
                  : "text-muted-foreground hover:bg-muted hover:text-foreground"
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

        <div className="mt-8 px-4 pb-2 text-[10px] font-semibold tracking-[0.15em] text-muted-foreground uppercase">
          System
        </div>
        <div className="mx-3 rounded-xl border border-border bg-muted/30 p-3.5">
          <div className="flex items-center justify-between text-[12px] font-medium text-foreground">
            <span className="flex items-center gap-2"><Activity className="size-3.5 text-emerald-400" /> API status</span>
            <span className={`size-2 rounded-full ${connected ? "bg-emerald-400" : "bg-rose-400"}`} />
          </div>
          <p className="mt-2 text-[11px] text-muted-foreground">{connected ? "Connected · localhost:5080" : "Waiting for API"}</p>
          <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-muted">
            <div className={`h-full rounded-full transition-all ${connected ? "w-full bg-emerald-400" : "w-1/4 bg-rose-400"}`} />
          </div>
        </div>

        <div className="mt-auto p-3">
          <div className="flex w-full items-center gap-3 rounded-lg p-2.5 text-left">
            <div className="grid size-8 place-items-center rounded-full bg-muted text-xs font-semibold text-foreground">TA</div>
            <div className="min-w-0 flex-1">
              <div className="truncate text-xs font-medium text-foreground">Security analyst</div>
              <div className="text-[10px] text-muted-foreground">Local workspace</div>
            </div>
          </div>
        </div>
      </aside>

      <div className="min-h-screen lg:pl-[248px]">
        <header className="sticky top-0 z-20 flex h-[68px] items-center justify-between border-b border-border bg-background/80 px-5 backdrop-blur-xl sm:px-8">
          <div className="flex items-center gap-3">
            <div className="grid size-8 place-items-center rounded-lg bg-primary text-primary-foreground lg:hidden"><Shield className="size-4" /></div>
            <div className="hidden text-xs text-muted-foreground sm:block">Workspace <ChevronRight className="mx-1 inline size-3" /></div>
            <div className="text-[13px] font-semibold text-foreground">{viewTitle}</div>
          </div>
          <div className="flex items-center gap-2 sm:gap-3">
            <div className="relative hidden w-[250px] md:block">
              <Search className="absolute top-1/2 left-3 size-3.5 -translate-y-1/2 text-muted-foreground" />
              <Input
                ref={searchInput}
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                onKeyDown={onSearchKeyDown}
                placeholder="Search events…"
                aria-label="Search events, users or IPs"
                aria-keyshortcuts="/"
                className="h-9 border-border bg-muted/40 pr-9 pl-9 text-xs shadow-none focus-visible:bg-card"
              />
              {query
                ? <button type="button" aria-label="Clear search" onClick={() => setQuery("")} className="absolute top-1/2 right-2 grid size-6 -translate-y-1/2 place-items-center rounded text-muted-foreground hover:bg-muted hover:text-foreground/90"><X className="size-3.5" /></button>
                : <kbd className="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2 rounded border border-border bg-card px-1.5 font-mono text-[10px] text-muted-foreground">/</kbd>}
            </div>
            <Button variant="ghost" size="icon" className="relative text-muted-foreground" aria-label={detectionCount > 0 ? `${detectionCount} detection(s) to review` : "Open investigations"} title={detectionCount > 0 ? `${detectionCount} detection(s) to review` : "No detections"} onClick={() => setActiveView("investigations")}>
              <Bell className="size-[17px]" />
              {detectionCount > 0 && <span className="absolute top-1.5 right-1.5 size-1.5 rounded-full bg-rose-500 ring-2 ring-background" />}
            </Button>
            <Separator orientation="vertical" className="mx-1 hidden h-6 sm:block" />
            <Button variant="outline" className="h-9 gap-2 border-border bg-card px-3 text-xs shadow-sm" onClick={() => void refresh()} disabled={loading}>
              <RefreshCw className={`size-3.5 ${loading ? "animate-spin" : ""}`} />
              <span className="hidden sm:inline">Refresh</span>
            </Button>
          </div>
        </header>

        <div className="border-b border-border bg-card px-3 py-2 lg:hidden">
          <nav ref={mobileNav} className="flex gap-1 overflow-x-auto">
            {navigation.map(({ id, label, icon: Icon }) => (
              <button key={id} type="button" onClick={() => setActiveView(id)} aria-current={activeView === id ? "page" : undefined} className={`flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-xs transition focus-visible:ring-2 focus-visible:ring-blue-500/40 focus-visible:outline-none ${activeView === id ? "bg-muted font-semibold text-foreground" : "text-muted-foreground hover:bg-muted/40 hover:text-foreground"}`}>
                <Icon className="size-3.5" />{label}
              </button>
            ))}
          </nav>
        </div>
        <div className="border-b border-border bg-card px-4 pb-3 md:hidden">
          <div className="relative">
            <Search className="absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" />
            <Input ref={mobileSearchInput} value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={onSearchKeyDown} placeholder="Search events, users, IPs..." aria-label="Search events" className="h-10 border-border bg-muted/40 pr-10 pl-10 text-sm" />
            {query && <button type="button" aria-label="Clear search" onClick={() => setQuery("")} className="absolute top-1/2 right-2 grid size-7 -translate-y-1/2 place-items-center rounded text-muted-foreground hover:bg-muted hover:text-foreground/90"><X className="size-4" /></button>}
          </div>
        </div>

        <main className="mx-auto max-w-[1500px] p-5 sm:p-8">
          <div className="mb-7 flex flex-col justify-between gap-4 md:flex-row md:items-end">
            <div>
              <h1 className="text-[25px] font-semibold tracking-[-0.04em] text-foreground">{viewTitle}</h1>
              <p className="mt-1 text-[13px] text-muted-foreground">
                {activeView === "overview" && "Monitor activity, detections and investigation workflows."}
                {activeView === "assistant" && "Ask questions in plain language; answers cite the events they rely on."}
                {activeView === "investigations" && "Evidence-led analysis from your deterministic agent pipeline."}
                {activeView === "events" && "Search and review normalized security events."}
                {activeView === "sources" && "Connected log sources and their latest activity."}
                {activeView === "settings" && "Runtime status, storage and analysis configuration."}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <Dialog open={importOpen} onOpenChange={setImportOpen}>
                <DialogTrigger asChild>
                  <Button variant="outline" className="h-9 gap-2 border-border bg-card text-xs shadow-sm">
                    <Upload className="size-3.5" /> Import logs
                  </Button>
                </DialogTrigger>
                <DialogContent className="sm:max-w-xl">
                  <DialogHeader>
                    <DialogTitle>Import log events</DialogTitle>
                    <DialogDescription>Import pipe-delimited or <code className="font-mono text-[11px]">timestamp LEVEL [component] message</code> lines. Re-importing the same file does not create duplicates; invalid rows are reported after processing.</DialogDescription>
                  </DialogHeader>
                  <div className="space-y-4 py-2">
                    <label
                      onDragOver={(event) => { event.preventDefault(); setFileDropActive(true) }}
                      onDragLeave={(event) => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setFileDropActive(false) }}
                      onDrop={(event) => { event.preventDefault(); setFileDropActive(false); void importFile(event.dataTransfer.files[0]) }}
                      className={`relative flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed px-5 py-7 text-center transition ${fileDropActive ? "border-blue-500 bg-blue-500/10 ring-2 ring-blue-500/10" : "border-foreground/20 bg-muted/40 hover:border-blue-500/50 hover:bg-blue-500/5"}`}
                    >
                      <Upload className="mb-2 size-5 text-muted-foreground" />
                      <span className="text-sm font-medium text-foreground/90">{content ? "Choose another file" : "Choose a .log or .txt file"}</span>
                      <span className="mt-1 text-xs text-muted-foreground">or drop a file here</span>
                      <input type="file" accept=".log,.txt,text/plain" className="sr-only" onChange={(event) => { void importFile(event.target.files?.[0]); event.currentTarget.value = "" }} />
                    </label>
                    {content && <div className="flex items-center justify-between rounded-lg border border-blue-500/20 bg-blue-500/10 px-3 py-2 text-xs"><span className="truncate font-medium text-blue-300">{source} <span className="font-normal text-blue-400">· {content.split(/\r?\n/).filter((line) => line.trim()).length.toLocaleString("en-US")} lines</span></span><button type="button" onClick={() => { setContent(""); setSource("manual-import") }} className="ml-2 shrink-0 rounded px-2 py-1 text-blue-400 hover:bg-blue-500/15">Clear</button></div>}
                    <div className="grid gap-2">
                      <label htmlFor="source-name" className="text-xs font-medium text-foreground/70">Source name</label>
                      <Input id="source-name" value={source} onChange={(event) => setSource(event.target.value)} placeholder="manual-import" />
                    </div>
                    <div className="grid gap-2">
                      <label htmlFor="log-content" className="text-xs font-medium text-foreground/70">Or paste log lines</label>
                      <textarea id="log-content" value={content} onChange={(event) => setContent(event.target.value)} rows={7} placeholder="2026-09-25T17:00:30Z WARN [security] Failed authentication attempt username=admin source=10.10.20.15" className="resize-y rounded-lg border border-border bg-card px-3 py-2 font-mono text-xs leading-5 outline-none placeholder:text-muted-foreground focus-visible:ring-2 focus-visible:ring-blue-500/20" />
                    </div>
                  </div>
                  <DialogFooter>
                    <Button variant="outline" onClick={() => setImportOpen(false)}>Cancel</Button>
                    <Button onClick={() => void submitImport()} disabled={importing || !content.trim()} className="gap-2">
                      {importing ? <LoaderCircle className="size-4 animate-spin" /> : <Upload className="size-4" />}
                      {importing ? "Importing..." : "Import events"}
                    </Button>
                  </DialogFooter>
                </DialogContent>
              </Dialog>
              <Button onClick={() => void startInvestigation()} disabled={investigating} className="h-9 gap-2 bg-primary px-3.5 text-xs text-primary-foreground hover:bg-primary/90">
                {investigating ? <LoaderCircle className="size-3.5 animate-spin" /> : <Sparkles className="size-3.5" />}
                {investigating ? "Investigating..." : "New investigation"}
              </Button>
            </div>
          </div>

          {!connected && !loading && (
            <div className="mb-6 flex items-center gap-3 rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-300">
              <AlertTriangle className="size-4 shrink-0" />
              <span className="flex-1">API unreachable. Start the Agentic Log Analyzer API on <code className="font-mono">localhost:5080</code>, then retry.</span>
              <Button variant="outline" size="sm" onClick={() => void refresh()} className="h-8 gap-1.5 border-rose-500/30 bg-card text-xs text-rose-400 hover:bg-rose-500/15"><RefreshCw className="size-3.5" />Retry</Button>
            </div>
          )}

          <div className={activeView === "assistant" ? undefined : "hidden"}><LogChat /></div>
          {activeView !== "assistant" && (
            <div key={activeView} className="animate-in duration-300 fade-in-0 slide-in-from-bottom-1">
              {activeView === "overview" && (
                <OverviewView
                  events={events}
                  recentEvents={searchedEvents}
                  report={report}
                  activity={activity.buckets}
                  activityStep={activity.step}
                  categoryData={categoryData}
                  loading={loading}
                  connected={connected}
                  failureCount={failureCount}
                  sourceCount={sources.length}
                  onImport={() => setImportOpen(true)}
                  onShowEvents={showEvents}
                  onShowView={setActiveView}
                  onSelectEvent={setSelectedEvent}
                />
              )}
              {activeView === "investigations" && <InvestigationView report={report} loading={loading || investigating} onInvestigate={() => void startInvestigation()} onSelectEvent={setSelectedEvent} onFilterEntity={(value) => showEvents("all", value)} />}
              {activeView === "events" && (
                <EventsView
                  events={visibleEvents}
                  loading={loading}
                  query={query}
                  resultFilter={resultFilter}
                  resultCounts={resultCounts}
                  onResultFilterChange={setResultFilter}
                  onClearFilters={() => { setQuery(""); setResultFilter("all") }}
                  onSelectEvent={setSelectedEvent}
                />
              )}
              {activeView === "sources" && <SourcesView sources={sources} loading={loading} onDelete={removeEvents} onShowSource={(name) => showEvents("all", name)} onImport={() => setImportOpen(true)} />}
              {activeView === "settings" && <SettingsView apiConnected={connected} eventCount={events.length} detectionCount={detectionCount} />}
            </div>
          )}

          <EventDetailDialog
            event={selectedEvent}
            onOpenChange={(open) => { if (!open) setSelectedEvent(null) }}
            onFilter={(value) => { setSelectedEvent(null); showEvents("all", value) }}
          />

          <footer className="mt-10 flex flex-col justify-between gap-2 border-t border-border pt-5 text-[11px] text-muted-foreground sm:flex-row">
            <span>Agentic Log Analyzer <span className="mx-1.5">·</span> Deterministic engine</span>
            <span className="flex items-center gap-2"><span className={`size-1.5 rounded-full ${connected ? "bg-emerald-500" : "bg-rose-500"}`} />{connected ? "All systems operational" : "API connection required"}<span className="mx-1.5">·</span>Local persistent storage</span>
          </footer>
        </main>
      </div>
    </div>
  )
}

function OverviewView({
  events,
  recentEvents,
  report,
  activity,
  activityStep,
  categoryData,
  loading,
  connected,
  failureCount,
  sourceCount,
  onImport,
  onShowEvents,
  onShowView,
  onSelectEvent,
}: {
  events: CanonicalEvent[]
  recentEvents: CanonicalEvent[]
  report: InvestigationReport | null
  activity: ActivityBucket[]
  activityStep: number
  categoryData: { name: string; count: number }[]
  loading: boolean
  connected: boolean
  failureCount: number
  sourceCount: number
  onImport: () => void
  onShowEvents: (filter?: ResultFilter) => void
  onShowView: (view: View) => void
  onSelectEvent: (event: CanonicalEvent) => void
}) {
  if (!loading && connected && events.length === 0) return <OnboardingPanel onImport={onImport} />

  const detections = report?.detections.length ?? 0
  const stats = [
    { label: "Events indexed", value: events.length.toLocaleString("en-US"), note: "Open the event stream", icon: Database, tone: "blue", onClick: () => onShowEvents("all") },
    { label: "Detections", value: String(detections), note: detections ? "Require analyst review" : "No active detections", icon: ShieldAlert, tone: "rose", onClick: () => onShowView("investigations") },
    { label: "Failed events", value: String(failureCount), note: "Show failures only", icon: AlertTriangle, tone: "amber", onClick: () => onShowEvents("failure") },
    { label: "Log sources", value: String(sourceCount), note: "Manage imported sources", icon: Network, tone: "violet", onClick: () => onShowView("sources") },
  ]
  const toneClass: Record<string, string> = {
    blue: "bg-blue-500/10 text-blue-400",
    rose: "bg-rose-500/10 text-rose-400",
    amber: "bg-amber-500/10 text-amber-400",
    violet: "bg-violet-500/10 text-violet-400",
  }
  const chartData = activity.map((bucket) => ({ ...bucket, other: bucket.events - bucket.failures - bucket.warnings }))

  return (
    <div className="space-y-6">
      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map(({ label, value, note, icon: Icon, tone, onClick }) => (
          <button
            key={label}
            type="button"
            onClick={onClick}
            className="group rounded-xl border border-border bg-card p-4 text-left shadow-[0_1px_2px_rgba(15,23,42,.03)] transition hover:-translate-y-0.5 hover:border-blue-500/30 hover:shadow-[0_6px_18px_rgba(15,23,42,.06)] focus-visible:ring-2 focus-visible:ring-blue-500/40 focus-visible:outline-none sm:p-5"
          >
            <div className="flex items-start justify-between">
              <div>
                <div className="text-[11px] font-medium text-muted-foreground">{label}</div>
                {loading ? <Skeleton className="mt-2 h-[26px] w-16" /> : <div className="mt-2 text-[26px] leading-none font-semibold tracking-[-0.04em] text-foreground tabular-nums">{value}</div>}
              </div>
              <div className={`grid size-9 place-items-center rounded-lg ${toneClass[tone]}`}><Icon className="size-[17px]" /></div>
            </div>
            <div className="mt-3 flex items-center gap-1 text-[10px] text-muted-foreground transition group-hover:text-blue-400">
              {note}<ArrowRight className="size-3 opacity-0 transition group-hover:translate-x-0.5 group-hover:opacity-100" />
            </div>
          </button>
        ))}
      </section>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1.65fr)_minmax(300px,.8fr)]">
        <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="px-5 pt-1">
            <CardTitle className="text-[13px] font-semibold text-foreground">Event activity</CardTitle>
            <CardDescription className="text-[11px]">Events per {describeStep(activityStep)}, by outcome</CardDescription>
            <CardAction className="flex items-center gap-3 text-[10px] text-muted-foreground">
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-[#1e3160]" />Other</span>
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-amber-400" />Warnings</span>
              <span className="flex items-center gap-1.5"><span className="size-2 rounded-full bg-rose-500" />Failures</span>
            </CardAction>
          </CardHeader>
          <CardContent className="px-2 pb-2 sm:px-4">
            {loading ? <Skeleton className="h-[248px] w-full" /> : chartData.length ? (
              <ResponsiveContainer width="100%" height={248}>
                <BarChart data={chartData} margin={{ top: 8, right: 12, left: -16, bottom: 0 }} barCategoryGap="18%">
                  <CartesianGrid stroke="#111a30" strokeDasharray="3 5" vertical={false} />
                  <XAxis dataKey="label" tickLine={false} axisLine={false} tick={{ fill: "#6b7a99", fontSize: 10 }} minTickGap={24} />
                  <YAxis allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: "#6b7a99", fontSize: 10 }} />
                  <Tooltip cursor={{ fill: "#ffffff0d" }} contentStyle={{ borderRadius: 10, borderColor: "#1a2542", backgroundColor: "#08101f", color: "#e6ecf7", fontSize: 11, boxShadow: "0 8px 24px #0f172a12" }} />
                  <Bar dataKey="other" name="Other" stackId="events" fill="#1e3160" />
                  <Bar dataKey="warnings" name="Warnings" stackId="events" fill="#fbbf24" />
                  <Bar dataKey="failures" name="Failures" stackId="events" fill="#f43f5e" radius={[4, 4, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <ChartEmpty message="Import logs to see event activity." />
            )}
          </CardContent>
        </Card>

        <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="px-5 pt-1">
            <CardTitle className="text-[13px] font-semibold text-foreground">Events by category</CardTitle>
            <CardDescription className="text-[11px]">Top categories in your dataset</CardDescription>
          </CardHeader>
          <CardContent className="px-2 pb-2">
            {loading ? <Skeleton className="h-[248px] w-full" /> : categoryData.length ? (
              <ResponsiveContainer width="100%" height={248}>
                <BarChart data={categoryData} layout="vertical" margin={{ top: 4, right: 18, left: 12, bottom: 0 }}>
                  <CartesianGrid stroke="#111a30" strokeDasharray="3 5" horizontal={false} />
                  <XAxis type="number" allowDecimals={false} tickLine={false} axisLine={false} tick={{ fill: "#6b7a99", fontSize: 10 }} />
                  <YAxis type="category" dataKey="name" width={80} tickLine={false} axisLine={false} tick={{ fill: "#8b9ab8", fontSize: 10 }} />
                  <Tooltip cursor={{ fill: "#ffffff0d" }} contentStyle={{ borderRadius: 10, borderColor: "#1a2542", backgroundColor: "#08101f", color: "#e6ecf7", fontSize: 11 }} />
                  <Bar dataKey="count" name="Events" fill="#60a5fa" radius={[0, 5, 5, 0]} barSize={16} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <ChartEmpty message="Category distribution appears after ingestion." />
            )}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1.65fr)_minmax(300px,.8fr)]">
        <Card className="gap-0 overflow-hidden rounded-xl border-border py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="border-b border-border px-5 py-4">
            <CardTitle className="text-[13px] font-semibold text-foreground">Recent events</CardTitle>
            <CardDescription className="text-[11px]">Latest normalized activity · click a row for details</CardDescription>
            <CardAction>
              <Button variant="ghost" size="sm" onClick={() => onShowEvents("all")} className="h-8 gap-1 px-2 text-[11px] text-blue-400">View all <ArrowRight className="size-3" /></Button>
            </CardAction>
          </CardHeader>
          <EventTable events={recentEvents.slice(0, 6)} loading={loading} compact onSelect={onSelectEvent} />
        </Card>

        <Card className="gap-0 rounded-xl border-border py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="px-5 pt-5 pb-3">
            <CardTitle className="text-[13px] font-semibold text-foreground">Detection queue</CardTitle>
            <CardDescription className="text-[11px]">Rule-based signals for review</CardDescription>
            <CardAction><Badge variant="secondary" className="bg-muted text-[10px] text-foreground/70">{detections} total</Badge></CardAction>
          </CardHeader>
          <CardContent className="space-y-3 px-5 pt-1 pb-5">
            {loading ? <Skeleton className="h-24 w-full" /> : report?.detections.length ? report.detections.slice(0, 3).map((detection) => (
              <button key={detection.ruleId + detection.evidenceEventIds[0]} type="button" onClick={() => onShowView("investigations")} className="w-full rounded-lg border border-rose-500/20 bg-rose-500/10 p-3 text-left transition hover:border-rose-500/30 hover:bg-rose-500/10 focus-visible:ring-2 focus-visible:ring-rose-400/40 focus-visible:outline-none">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex gap-2.5">
                    <div className="mt-0.5 grid size-7 shrink-0 place-items-center rounded-md bg-rose-500/15 text-rose-400"><ShieldAlert className="size-3.5" /></div>
                    <div><div className="text-[11px] font-semibold text-foreground">{detection.title}</div><div className="mt-1 line-clamp-2 text-[10px] leading-4 text-muted-foreground">{detection.description}</div></div>
                  </div>
                  <Badge variant="destructive" className="h-5 px-1.5 text-[9px] uppercase">{detection.severity}</Badge>
                </div>
                <div className="mt-2.5 flex items-center justify-between pl-9 text-[9px] text-muted-foreground"><span>{detection.ruleId} · {detection.evidenceEventIds.length} evidence events</span><span>{formatShortTime(detection.lastSeen)}</span></div>
              </button>
            )) : (
              <div className="flex min-h-32 flex-col items-center justify-center rounded-lg border border-dashed border-border bg-muted/30 px-5 text-center">
                <Shield className="mb-2 size-5 text-muted-foreground/60" />
                <p className="text-[11px] font-medium text-foreground/70">No active detections</p>
                <p className="mt-1 text-[10px] text-muted-foreground">Deterministic rules are checked on every investigation.</p>
              </div>
            )}
            <button type="button" onClick={() => onShowView("investigations")} className="flex w-full items-center justify-between rounded-md border-t border-border pt-3 text-[10px] font-medium text-muted-foreground transition hover:text-blue-400 focus-visible:text-blue-400 focus-visible:outline-none">
              Open investigation workspace <ArrowRight className="size-3" />
            </button>
          </CardContent>
        </Card>
      </section>
    </div>
  )
}

function OnboardingPanel({ onImport }: { onImport: () => void }) {
  const steps = [
    { icon: Upload, title: "Import a log file", detail: "Drop a .log or .txt file, or paste lines. Re-imports never duplicate events." },
    { icon: ShieldAlert, title: "Review detections", detail: "Deterministic rules flag brute force and other patterns with evidence." },
    { icon: Bot, title: "Ask the assistant", detail: "Ask questions in plain language; every answer cites its events." },
  ]
  return (
    <section className="relative overflow-hidden rounded-2xl border border-border bg-card px-6 py-10 shadow-[0_1px_2px_rgba(15,23,42,.03)] sm:px-10">
      <div className="absolute -top-24 -right-24 size-72 rounded-full bg-blue-500/10" />
      <div className="relative max-w-2xl">
        <div className="mb-4 grid size-12 place-items-center rounded-2xl bg-primary text-primary-foreground"><FileText className="size-6" /></div>
        <h2 className="text-xl font-semibold tracking-tight text-foreground">No events yet</h2>
        <p className="mt-1.5 text-[13px] leading-6 text-muted-foreground">Import your first logs to see activity, detections and investigations. Supported lines look like:</p>
        <pre className="mt-3 overflow-x-auto rounded-lg border border-border bg-muted/40 px-3 py-2 font-mono text-[11px] text-foreground/70">2026-09-25T17:00:30Z WARN [security] Failed authentication attempt username=admin source=10.10.20.15</pre>
        <Button onClick={onImport} className="mt-5 h-10 gap-2 bg-primary px-4 text-sm text-primary-foreground hover:bg-primary/90"><Upload className="size-4" />Import logs</Button>
      </div>
      <div className="relative mt-8 grid gap-3 sm:grid-cols-3">
        {steps.map(({ icon: Icon, title, detail }, index) => (
          <div key={title} className="rounded-xl border border-border bg-muted/30 p-4">
            <div className="flex items-center gap-2 text-[11px] font-semibold text-foreground/90"><span className="grid size-5 place-items-center rounded-full bg-card text-[10px] text-blue-400 shadow-sm">{index + 1}</span><Icon className="size-3.5 text-muted-foreground" />{title}</div>
            <p className="mt-2 text-[11px] leading-5 text-muted-foreground">{detail}</p>
          </div>
        ))}
      </div>
    </section>
  )
}

function ChartEmpty({ message }: { message: string }) {
  return <div className="grid h-[248px] place-items-center text-center text-xs text-muted-foreground">{message}</div>
}

function EventTable({
  events,
  loading,
  compact = false,
  onSelect,
  emptyAction,
}: {
  events: CanonicalEvent[]
  loading: boolean
  compact?: boolean
  onSelect: (event: CanonicalEvent) => void
  emptyAction?: { label: string; onClick: () => void }
}) {
  if (loading && !events.length) {
    return (
      <div className="space-y-3 p-5">
        {Array.from({ length: compact ? 4 : 8 }, (_, index) => (
          <div key={index} className="flex items-center gap-3"><Skeleton className="size-7 rounded-md" /><Skeleton className="h-4 flex-1" /><Skeleton className="h-5 w-16 rounded-full" /><Skeleton className="h-4 w-24" /></div>
        ))}
      </div>
    )
  }
  if (!events.length) {
    return (
      <div className="p-10 text-center">
        <FileSearch className="mx-auto mb-2 size-5 text-muted-foreground/60" />
        <div className="text-xs font-medium text-foreground/70">No events found</div>
        <div className="mt-1 text-[10px] text-muted-foreground">Try a different search or import a log file.</div>
        {emptyAction && <Button variant="outline" size="sm" onClick={emptyAction.onClick} className="mt-3 h-8 text-xs">{emptyAction.label}</Button>}
      </div>
    )
  }
  return (
    <div className="overflow-x-auto">
      <Table>
        <TableHeader>
          <TableRow className="border-border hover:bg-transparent">
            <TableHead className="h-9 pl-5 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">Event</TableHead>
            <TableHead className="h-9 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">Outcome</TableHead>
            <TableHead className="h-9 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">Identity</TableHead>
            {!compact && <TableHead className="h-9 text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">Source</TableHead>}
            <TableHead className="h-9 pr-5 text-right text-[10px] font-semibold tracking-wide text-muted-foreground uppercase">Time</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {events.map((event) => (
            <TableRow key={event.id} onClick={() => onSelect(event)} className="group cursor-pointer border-border hover:bg-blue-500/5">
              <TableCell className="max-w-[320px] py-3 pl-5">
                <div className="flex items-center gap-2.5">
                  <div className={`grid size-7 shrink-0 place-items-center rounded-md ${resultTone(event.result) === "danger" ? "bg-rose-500/10 text-rose-400" : resultTone(event.result) === "warning" ? "bg-amber-500/10 text-amber-400" : "bg-blue-500/10 text-blue-400"}`}>
                    {event.category.toLowerCase().includes("auth") || event.category.toLowerCase().includes("security") ? <Fingerprint className="size-3.5" /> : <Activity className="size-3.5" />}
                  </div>
                  <div className="min-w-0">
                    <button
                      type="button"
                      onClick={(clickEvent) => { clickEvent.stopPropagation(); onSelect(event) }}
                      className="block max-w-full truncate rounded text-left text-[11px] font-medium text-foreground group-hover:text-blue-400 focus-visible:ring-2 focus-visible:ring-blue-500/40 focus-visible:outline-none"
                    >
                      {event.category} <span className="font-normal text-muted-foreground">/</span> {event.action}
                    </button>
                    <div className="truncate font-mono text-[10px] text-muted-foreground">{event.rawContent}</div>
                  </div>
                </div>
              </TableCell>
              <TableCell><ResultBadge result={event.result} /></TableCell>
              <TableCell><IdentityCell event={event} /></TableCell>
              {!compact && <TableCell><div className="max-w-[160px] truncate text-[11px] text-foreground/70" title={event.sourceName}>{event.sourceName}</div><div className="text-[10px] text-muted-foreground">{event.sourceType}</div></TableCell>}
              <TableCell className="pr-5 text-right"><div className="text-[10px] whitespace-nowrap text-muted-foreground tabular-nums">{formatTimestamp(event.timestamp)}</div></TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function IdentityCell({ event }: { event: CanonicalEvent }) {
  const items = [
    event.user && { icon: UserRound, value: event.user, mono: false },
    event.sourceIp && { icon: Globe2, value: event.sourceIp, mono: true },
    event.device && { icon: Monitor, value: event.device, mono: false },
  ].filter(Boolean) as { icon: typeof UserRound; value: string; mono: boolean }[]
  if (!items.length) return <span className="text-[11px] text-muted-foreground/60">—</span>
  return (
    <div className="space-y-0.5">
      {items.slice(0, 2).map(({ icon: Icon, value, mono }) => (
        <div key={value} className={`flex max-w-[180px] items-center gap-1.5 text-[11px] text-foreground/70 ${mono ? "font-mono" : ""}`}><Icon className="size-3 shrink-0 text-muted-foreground" /><span className="truncate">{value}</span></div>
      ))}
    </div>
  )
}

function InvestigationView({
  report,
  loading,
  onInvestigate,
  onSelectEvent,
  onFilterEntity,
}: {
  report: InvestigationReport | null
  loading: boolean
  onInvestigate: () => void
  onSelectEvent: (event: CanonicalEvent) => void
  onFilterEntity: (value: string) => void
}) {
  if (!report && loading) {
    return <div className="space-y-4"><Skeleton className="h-28 w-full rounded-xl" /><div className="grid gap-4 xl:grid-cols-2"><Skeleton className="h-64 rounded-xl" /><Skeleton className="h-64 rounded-xl" /></div></div>
  }
  if (!report) {
    return (
      <div className="grid min-h-72 place-items-center rounded-xl border border-dashed border-border bg-card text-center">
        <div>
          <Sparkles className="mx-auto mb-2 size-6 text-blue-400" />
          <p className="text-sm font-medium text-foreground/90">No investigation yet</p>
          <p className="mt-1 text-xs text-muted-foreground">Run one to generate an evidence-backed report.</p>
          <Button onClick={onInvestigate} className="mt-4 h-9 gap-2 bg-primary text-xs text-primary-foreground hover:bg-primary/90"><Sparkles className="size-3.5" />Start investigation</Button>
        </div>
      </div>
    )
  }
  const eventsById = new Map(report.events.map((event) => [event.id, event]))

  return (
    <div className="space-y-5">
      <Card className="overflow-hidden rounded-xl border-blue-500/20 bg-gradient-to-br from-card to-blue-500/5 shadow-[0_2px_10px_rgba(37,99,235,.04)]">
        <CardContent className="flex flex-col justify-between gap-5 p-5 sm:flex-row sm:items-center sm:p-6">
          <div>
            <div className="mb-2 flex items-center gap-2"><Badge className="bg-primary text-[9px] tracking-wide text-primary-foreground">COMPLETED</Badge><span className="text-[10px] text-muted-foreground">{formatTimestamp(report.createdAt)}</span></div>
            <h2 className="text-lg font-semibold tracking-tight text-foreground">Investigation report</h2>
            <p className="mt-1 text-xs text-muted-foreground">{report.summary}</p>
          </div>
          <div className="flex gap-5 sm:gap-7">
            <MiniMetric label="Events reviewed" value={report.events.length} />
            <MiniMetric label="Detections" value={report.detections.length} highlight={report.detections.length > 0} />
            <MiniMetric label="Correlations" value={report.correlations.length} />
          </div>
        </CardContent>
      </Card>

      <section className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,.8fr)]">
        <Card className="gap-0 overflow-hidden rounded-xl border-border py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
          <CardHeader className="border-b border-border px-5 py-4">
            <div className="flex items-center gap-2"><ShieldAlert className="size-4 text-rose-400" /><CardTitle className="text-[13px] font-semibold">Deterministic detections</CardTitle></div>
            <CardDescription className="text-[11px]">Every finding includes the event IDs used as evidence.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 p-4">
            {report.detections.length ? report.detections.map((detection) => (
              <div key={detection.ruleId + detection.evidenceEventIds[0]} className="rounded-xl border border-rose-500/20 bg-rose-500/5 p-4">
                <div className="flex flex-wrap items-center gap-2"><Badge variant="destructive" className="text-[9px] uppercase">{detection.severity}</Badge><span className="font-mono text-[10px] text-muted-foreground">{detection.ruleId}</span><span className="ml-auto text-[10px] text-muted-foreground">{formatTimestamp(detection.firstSeen)} – {formatShortTime(detection.lastSeen)}</span></div>
                <h3 className="mt-2 text-[13px] font-semibold text-foreground">{detection.title}</h3>
                <p className="mt-1 text-[11px] leading-5 text-foreground/70">{detection.description}</p>
                <div className="mt-3 flex flex-wrap gap-2">{detection.user && <Badge variant="outline" className="bg-card text-[10px]"><UserRound className="mr-1 size-3" />{detection.user}</Badge>}{detection.sourceIp && <Badge variant="outline" className="bg-card font-mono text-[10px]"><Globe2 className="mr-1 size-3" />{detection.sourceIp}</Badge>}<Badge variant="outline" className="bg-card text-[10px]">{detection.evidenceEventIds.length} evidence events</Badge></div>
                <details className="mt-3 border-t border-rose-500/20 pt-2">
                  <summary className="text-[10px] font-medium text-rose-400 hover:text-rose-300">View evidence events</summary>
                  <div className="mt-2 space-y-1">
                    {detection.evidenceEventIds.map((id) => {
                      const evidence = eventsById.get(id)
                      return evidence ? (
                        <button key={id} type="button" onClick={() => onSelectEvent(evidence)} className="flex w-full items-center justify-between gap-3 rounded-md border border-rose-500/20 bg-card px-2.5 py-1.5 text-left text-[10px] transition hover:border-rose-500/30 hover:bg-rose-500/10 focus-visible:ring-2 focus-visible:ring-rose-400/40 focus-visible:outline-none">
                          <span className="truncate text-foreground/90">{evidence.action}</span>
                          <span className="shrink-0 text-muted-foreground tabular-nums">{formatTimestamp(evidence.timestamp)}</span>
                        </button>
                      ) : <div key={id} className="font-mono text-[9px] text-muted-foreground">{id}</div>
                    })}
                  </div>
                </details>
              </div>
            )) : <EmptyPanel icon={Shield} title="No rule matched" detail="No configured deterministic rule matched the selected events." />}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
            <CardHeader className="px-5 pt-5 pb-2"><div className="flex items-center gap-2"><Network className="size-4 text-violet-400" /><CardTitle className="text-[13px] font-semibold">Correlated entities</CardTitle></div><CardDescription className="text-[11px]">Shared identities and infrastructure in this result set.</CardDescription></CardHeader>
            <CardContent className="space-y-2 px-5 pb-5">
              {report.correlations.length ? report.correlations.slice(0, 8).map((correlation) => {
                const Icon = correlation.entityType === "user" ? UserRound : correlation.entityType === "sourceIp" ? Globe2 : Server
                return <button key={`${correlation.entityType}-${correlation.entityValue}`} type="button" onClick={() => onFilterEntity(correlation.entityValue)} title={`Show events for ${correlation.entityValue}`} className="group flex w-full items-center gap-3 rounded-lg border border-border bg-muted/30 p-2.5 text-left transition hover:border-blue-500/30 hover:bg-blue-500/10 focus-visible:ring-2 focus-visible:ring-blue-500/40 focus-visible:outline-none"><div className="grid size-8 place-items-center rounded-md bg-card text-muted-foreground shadow-sm"><Icon className="size-3.5" /></div><div className="min-w-0 flex-1"><div className="truncate text-[11px] font-medium text-foreground/90">{correlation.entityValue}</div><div className="text-[9px] text-muted-foreground">Shared {correlation.entityType} · {correlation.eventIds.length} events</div></div><ChevronRight className="size-3.5 text-muted-foreground/60 transition group-hover:translate-x-0.5 group-hover:text-blue-400" /></button>
              }) : <p className="py-5 text-center text-[11px] text-muted-foreground">No repeated entities among these events.</p>}
            </CardContent>
          </Card>

          <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
            <CardHeader className="px-5 pt-5 pb-2"><div className="flex items-center gap-2"><Sparkles className="size-4 text-amber-400" /><CardTitle className="text-[13px] font-semibold">Assessment</CardTitle></div><CardDescription className="text-[11px]">Facts and hypotheses are kept separate.</CardDescription></CardHeader>
            <CardContent className="space-y-4 px-5 pb-5">
              <div><div className="mb-2 text-[9px] font-semibold tracking-wider text-emerald-400 uppercase">Observed facts</div>{report.facts.length ? report.facts.map((fact, index) => <div key={index} className="mb-2 rounded-md border-l-2 border-emerald-400 bg-emerald-500/10 px-3 py-2 text-[10px] leading-4 text-foreground/70">{fact.statement}<div className="mt-1 text-[9px] text-muted-foreground">Evidence: {fact.evidenceEventIds.length} event ID(s)</div></div>) : <p className="text-[10px] text-muted-foreground">No detection facts for this query.</p>}</div>
              <div><div className="mb-2 text-[9px] font-semibold tracking-wider text-amber-400 uppercase">Hypotheses to verify</div>{report.hypotheses.length ? report.hypotheses.map((item, index) => <div key={index} className="rounded-md border-l-2 border-amber-400 bg-amber-500/10 px-3 py-2 text-[10px] leading-4 text-foreground/70">{item.statement}</div>) : <p className="text-[10px] text-muted-foreground">No hypotheses generated.</p>}</div>
              {report.recommendations.length > 0 && <div><div className="mb-2 text-[9px] font-semibold tracking-wider text-blue-400 uppercase">Recommended next steps</div><ul className="space-y-1.5">{report.recommendations.map((item, index) => <li key={index} className="flex gap-2 text-[10px] leading-4 text-foreground/70"><ArrowRight className="mt-0.5 size-3 shrink-0 text-blue-400" />{item}</li>)}</ul></div>}
            </CardContent>
          </Card>
        </div>
      </section>

      <Card className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]">
        <CardHeader className="px-5 pt-5 pb-3"><div className="flex items-center gap-2"><WorkflowIcon /><CardTitle className="text-[13px] font-semibold">Orchestrator trace</CardTitle></div><CardDescription className="text-[11px]">Fixed sequence · deterministic tools · no LLM call</CardDescription></CardHeader>
        <CardContent className="grid gap-3 px-5 pb-5 sm:grid-cols-2 xl:grid-cols-4">
          {report.agentTrace.map((step, index) => (
            <div key={step.agent} className="relative flex items-center gap-3 rounded-lg border border-border bg-muted/30 p-3">
              {index < report.agentTrace.length - 1 && <div className="absolute top-1/2 -right-3 z-10 hidden h-px w-3 bg-muted-foreground/40 xl:block" />}
              <div className="grid size-8 shrink-0 place-items-center rounded-lg bg-card text-blue-400 shadow-sm">{index === 0 ? <FileSearch className="size-4" /> : index === 1 ? <ShieldAlert className="size-4" /> : index === 2 ? <Network className="size-4" /> : <FileClock className="size-4" />}</div>
              <div className="min-w-0"><div className="truncate text-[10px] font-semibold text-foreground/90">{step.agent}</div><div className="mt-0.5 flex items-center gap-1.5 text-[9px] text-muted-foreground"><Check className="size-3 text-emerald-400" />{step.outcome} · {step.evidenceCount} result(s)</div></div>
            </div>
          ))}
        </CardContent>
        <div className="flex flex-wrap items-center gap-x-5 gap-y-2 border-t border-border px-5 py-3 text-[9px] text-muted-foreground"><span>Investigation ID: <code className="font-mono text-muted-foreground">{report.id}</code></span><span>LLM used: <strong className="font-medium text-foreground/70">{report.llmUsed ? "yes" : "no"}</strong></span><span>Events analyzed: <strong className="font-medium text-foreground/70">{report.events.length}</strong></span></div>
      </Card>
    </div>
  )
}

function WorkflowIcon() {
  return <Layers3 className="size-4 text-blue-400" />
}

function MiniMetric({ label, value, highlight = false }: { label: string; value: number; highlight?: boolean }) {
  return <div className="min-w-[70px]"><div className="text-[9px] font-medium tracking-wide text-muted-foreground uppercase">{label}</div><div className={`mt-1 text-[20px] font-semibold tracking-tight ${highlight ? "text-rose-400" : "text-foreground"}`}>{value}</div></div>
}

function EmptyPanel({ icon: Icon, title, detail }: { icon: typeof Shield; title: string; detail: string }) {
  return <div className="grid min-h-32 place-items-center rounded-lg border border-dashed border-border bg-muted/30 px-5 text-center"><div><Icon className="mx-auto mb-2 size-5 text-muted-foreground/60" /><p className="text-[11px] font-medium text-foreground/70">{title}</p><p className="mt-1 text-[10px] text-muted-foreground">{detail}</p></div></div>
}

function EventsView({
  events,
  loading,
  query,
  resultFilter,
  resultCounts,
  onResultFilterChange,
  onClearFilters,
  onSelectEvent,
}: {
  events: CanonicalEvent[]
  loading: boolean
  query: string
  resultFilter: ResultFilter
  resultCounts: Record<ResultFilter, number>
  onResultFilterChange: (filter: ResultFilter) => void
  onClearFilters: () => void
  onSelectEvent: (event: CanonicalEvent) => void
}) {
  const [page, setPage] = useState(1)
  const pageSize = 25
  const pageCount = Math.max(1, Math.ceil(events.length / pageSize))
  const currentPage = Math.min(page, pageCount)
  const pageEvents = events.slice((currentPage - 1) * pageSize, currentPage * pageSize)
  const filtered = Boolean(query) || resultFilter !== "all"

  const changeFilter = (filter: ResultFilter) => {
    setPage(1)
    onResultFilterChange(filter)
  }

  return (
    <Card className="gap-0 overflow-hidden rounded-xl border-border py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
      <CardHeader className="border-b border-border px-5 py-4">
        <CardTitle className="text-[13px] font-semibold">Normalized event stream</CardTitle>
        <CardDescription className="text-[11px]">{events.length.toLocaleString("en-US")} events{query ? ` matching “${query}”` : " across all sources"} · click a row for details</CardDescription>
        {filtered && <CardAction><Button variant="ghost" size="sm" onClick={() => { setPage(1); onClearFilters() }} className="h-8 gap-1 text-[11px] text-foreground/70"><X className="size-3" />Clear filters</Button></CardAction>}
      </CardHeader>
      <div role="group" aria-label="Filter by outcome" className="flex gap-1.5 overflow-x-auto border-b border-border px-5 py-2.5">
        {resultFilters.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            aria-pressed={resultFilter === id}
            onClick={() => changeFilter(id)}
            className={`flex shrink-0 items-center gap-1.5 rounded-full border px-3 py-1 text-[11px] font-medium transition focus-visible:ring-2 focus-visible:ring-blue-500/40 focus-visible:outline-none ${resultFilter === id ? "border-primary bg-primary text-primary-foreground" : "border-border bg-transparent text-muted-foreground hover:border-foreground/30 hover:text-foreground"}`}
          >
            {label}
            <span className={`rounded-full px-1.5 text-[10px] tabular-nums ${resultFilter === id ? "bg-white/20 text-primary-foreground" : "bg-muted text-muted-foreground"}`}>{resultCounts[id]}</span>
          </button>
        ))}
      </div>
      <EventTable events={pageEvents} loading={loading} onSelect={onSelectEvent} emptyAction={filtered ? { label: "Clear filters", onClick: onClearFilters } : undefined} />
      {!loading && events.length > pageSize && <div className="flex flex-col gap-3 border-t border-border px-4 py-3 sm:flex-row sm:items-center sm:justify-between sm:px-5">
        <span className="text-[11px] text-muted-foreground">Showing {((currentPage - 1) * pageSize + 1).toLocaleString("en-US")}–{Math.min(currentPage * pageSize, events.length).toLocaleString("en-US")} of {events.length.toLocaleString("en-US")}</span>
        <div className="flex items-center gap-2">
          <Button variant="outline" size="sm" onClick={() => setPage(Math.max(1, currentPage - 1))} disabled={currentPage === 1} className="h-8 gap-1 text-xs"><ChevronLeft className="size-3.5" />Previous</Button>
          <span className="min-w-16 text-center text-[11px] text-muted-foreground tabular-nums">Page {currentPage} of {pageCount}</span>
          <Button variant="outline" size="sm" onClick={() => setPage(Math.min(pageCount, currentPage + 1))} disabled={currentPage === pageCount} className="h-8 gap-1 text-xs">Next<ChevronRight className="size-3.5" /></Button>
        </div>
      </div>}
    </Card>
  )
}

function SourcesView({
  sources,
  loading,
  onDelete,
  onShowSource,
  onImport,
}: {
  sources: { name: string; events: number; types: Set<string>; latest: string }[]
  loading: boolean
  onDelete: (source: string | null) => Promise<void>
  onShowSource: (name: string) => void
  onImport: () => void
}) {
  // undefined: dialog closed, null: delete everything, string: delete one source.
  const [target, setTarget] = useState<string | null | undefined>(undefined)
  const [deleting, setDeleting] = useState(false)

  if (loading) return <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{Array.from({ length: 3 }, (_, index) => <Skeleton key={index} className="h-48 rounded-xl" />)}</div>
  if (!sources.length) {
    return (
      <div className="grid min-h-60 place-items-center rounded-xl border border-dashed border-border bg-card text-center">
        <div>
          <Database className="mx-auto mb-2 size-6 text-muted-foreground/60" />
          <p className="text-sm font-medium text-foreground/90">No log sources yet</p>
          <p className="mt-1 text-xs text-muted-foreground">Import a log file to register its source.</p>
          <Button onClick={onImport} className="mt-4 h-9 gap-2 bg-primary text-xs text-primary-foreground hover:bg-primary/90"><Upload className="size-3.5" />Import logs</Button>
        </div>
      </div>
    )
  }

  const total = sources.reduce((sum, source) => sum + source.events, 0)
  const pending = target === undefined ? 0 : target === null ? total : sources.find((source) => source.name === target)?.events ?? 0

  const confirm = async () => {
    if (target === undefined) return
    setDeleting(true)
    try {
      await onDelete(target)
      setTarget(undefined)
    } finally {
      setDeleting(false)
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-[12px] text-muted-foreground">{sources.length} source(s) · {total.toLocaleString("en-US")} events</p>
        <Button variant="outline" onClick={() => setTarget(null)} className="h-9 gap-2 border-rose-500/30 bg-card text-xs text-rose-400 shadow-sm hover:bg-rose-500/10 hover:text-rose-300">
          <Trash2 className="size-3.5" /> Delete all data
        </Button>
      </div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">{sources.map((source) => <Card key={source.name} className="rounded-xl border-border shadow-[0_1px_2px_rgba(15,23,42,.03)]"><CardContent className="p-5"><div className="flex items-start justify-between"><div className="grid size-10 place-items-center rounded-xl bg-blue-500/10 text-blue-400"><Server className="size-5" /></div><div className="flex items-center gap-1.5"><Badge variant="outline" className="border-blue-500/30 bg-blue-500/10 text-[9px] text-blue-400">IMPORTED</Badge><button type="button" aria-label={`Delete events from ${source.name}`} title="Delete this source's events" onClick={() => setTarget(source.name)} className="grid size-7 place-items-center rounded-md text-muted-foreground transition hover:bg-rose-500/10 hover:text-rose-400 focus-visible:ring-2 focus-visible:ring-rose-400/40 focus-visible:outline-none"><Trash2 className="size-3.5" /></button></div></div><h3 className="mt-4 truncate text-sm font-semibold text-foreground" title={source.name}>{source.name}</h3><p className="mt-1 text-[10px] text-muted-foreground">{[...source.types].join(", ")} connector</p><Separator className="my-4" /><div className="flex justify-between"><div><div className="text-[9px] tracking-wide text-muted-foreground uppercase">Events</div><div className="mt-1 text-lg font-semibold text-foreground">{source.events.toLocaleString("en-US")}</div></div><div className="text-right"><div className="text-[9px] tracking-wide text-muted-foreground uppercase">Last event</div><div className="mt-2 text-[10px] text-foreground/70">{formatTimestamp(source.latest)}</div></div></div><Button variant="outline" size="sm" onClick={() => onShowSource(source.name)} className="mt-4 h-8 w-full gap-1.5 text-xs">View events <ArrowRight className="size-3" /></Button></CardContent></Card>)}</div>

      <Dialog open={target !== undefined} onOpenChange={(open) => { if (!open && !deleting) setTarget(undefined) }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{target === null ? "Delete all data?" : "Delete this source?"}</DialogTitle>
            <DialogDescription>
              {target === null
                ? `All ${pending.toLocaleString("en-US")} stored events will be permanently deleted.`
                : `${pending.toLocaleString("en-US")} event(s) from "${target}" will be permanently deleted.`}{" "}
              This cannot be undone. A worker that is still following this file will ingest it again on restart.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTarget(undefined)} disabled={deleting}>Cancel</Button>
            <Button onClick={() => void confirm()} disabled={deleting} className="gap-2 bg-rose-600 text-white hover:bg-rose-700">
              {deleting ? <LoaderCircle className="size-4 animate-spin" /> : <Trash2 className="size-4" />}
              {deleting ? "Deleting..." : "Delete"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}

export default App
