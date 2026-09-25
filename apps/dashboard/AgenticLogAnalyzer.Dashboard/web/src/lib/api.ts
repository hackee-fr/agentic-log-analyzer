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

const apiBase = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080").replace(/\/$/, "")

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
