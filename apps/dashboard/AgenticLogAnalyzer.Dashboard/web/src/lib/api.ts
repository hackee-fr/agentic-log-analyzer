export type CanonicalEvent = {
  id: string
  timestamp: string
  sourceType: string
  sourceName: string
  category: string
  action: string
  result: string | null
  user: string | null
  device: string | null
  sourceIp: string | null
  rawContent: string
  /** Every key=value pair of the source line (lowercased keys); null when the format has none. */
  attributes: Record<string, string> | null
}

export type Detection = {
  ruleId: string
  title: string
  severity: string
  description: string
  evidenceEventIds: string[]
  firstSeen: string
  lastSeen: string
  user: string | null
  sourceIp: string | null
}

export type Correlation = {
  entityType: string
  entityValue: string
  eventIds: string[]
  firstSeen: string
  lastSeen: string
}

export type InvestigationReport = {
  id: string
  createdAt: string
  query: string
  summary: string
  events: CanonicalEvent[]
  detections: Detection[]
  correlations: Correlation[]
  facts: { statement: string; evidenceEventIds: string[] }[]
  hypotheses: { statement: string; evidenceEventIds: string[] }[]
  recommendations: string[]
  agentTrace: { agent: string; outcome: string; evidenceCount: number }[]
  llmUsed: boolean
}

declare global {
  interface Window {
    __APP_CONFIG__?: { apiBase?: string }
  }
}

// The desktop app sets apiBase to "" (API on the same origin); the web dashboard falls back to the build setting.
export const apiBase = (window.__APP_CONFIG__?.apiBase ?? import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080").replace(/\/$/, "")

/** Host shown to users, e.g. "localhost:5080" or "127.0.0.1:53712" in the desktop app. */
export const apiHost = new URL(apiBase || window.location.origin).host

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBase}${path}`, init)
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? `API error ${response.status}`)
  }
  return response.json() as Promise<T>
}

export function getEvents(query = "") {
  return request<CanonicalEvent[]>(`/api/events?q=${encodeURIComponent(query)}`)
}

export type ApiSettings = {
  environment: string
  storage: { provider: string; fileName: string; initialized: boolean }
  analysis: {
    deterministicEngineEnabled: boolean
    llmEnabled: boolean
    llmProvider: string | null
    llmModel: string | null
  }
}

export function getApiSettings() {
  return request<ApiSettings>("/api/settings")
}

export function ingestLogs(content: string, source: string) {
  return request<{ accepted: number; rejected: { line: number; reason: string }[] }>("/api/ingest", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ content, source }),
  })
}

export function runInvestigation(query = "", maxEvents = 500) {
  return request<InvestigationReport>("/api/investigations", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ query, maxEvents }),
  })
}

export type ChatAnswer = {
  question: string
  intent: string
  appliedFilters: string[]
  matchedEventCount: number
  answer: string
  deterministicAnswer: string
  evidence: CanonicalEvent[]
  detections: Detection[]
  llmUsed: boolean
  llmError: string | null
}

export function askQuestion(question: string) {
  return request<ChatAnswer>("/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question }),
  })
}

/** Deletes the events of one source, or every stored event when `source` is null. */
export function deleteEvents(source: string | null) {
  const query = source === null ? "all=true" : `source=${encodeURIComponent(source)}`
  return request<{ deleted: number; source: string | null }>(`/api/events?${query}`, { method: "DELETE" })
}

export type LlmStatus = {
  provider: string
  model: string
  reachable: boolean
  modelAvailable: boolean
  installedModels: string[]
  error: string | null
}

/** Live LLM availability; `enabled` is false when the API runs without an LLM provider. */
export function getLlmStatus() {
  return request<{ enabled: boolean; status: LlmStatus | null }>("/api/llm/status")
}

export type AppUpdateStatus = {
  installed: boolean
  currentVersion: string | null
  availableVersion: string | null
  state: "disabled" | "idle" | "checking" | "up-to-date" | "downloading" | "ready" | "error"
  progress: number
  error: string | null
  lastChecked: string | null
}

/** Desktop auto-update status; null in the web dashboard, where the route does not exist. */
export async function getAppUpdate(): Promise<AppUpdateStatus | null> {
  const response = await fetch(`${apiBase}/api/app/update`)
  if (response.status === 404) return null
  if (!response.ok) throw new Error(`API error ${response.status}`)
  return response.json() as Promise<AppUpdateStatus>
}

export function checkAppUpdate() {
  return request<AppUpdateStatus>("/api/app/update/check", { method: "POST" })
}

/** Installs the downloaded update; the desktop app exits and relaunches on the new version. */
export async function applyAppUpdate() {
  const response = await fetch(`${apiBase}/api/app/update/apply`, { method: "POST" })
  if (!response.ok) {
    const body = await response.json().catch(() => null)
    throw new Error(body?.error ?? `API error ${response.status}`)
  }
}
