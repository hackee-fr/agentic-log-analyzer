import { Check, Copy, Globe2, Monitor, Tag, UserRound } from "lucide-react"
import { useState, type ReactNode } from "react"
import { toast } from "sonner"

import type { CanonicalEvent } from "@/lib/api"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"

const formatFull = (value: string) =>
  new Intl.DateTimeFormat("en-GB", { dateStyle: "full", timeStyle: "medium" }).format(new Date(value))

/** Shows every canonical field of one event plus its raw line, with copy shortcuts. */
export function EventDetailDialog({
  event,
  onOpenChange,
  onFilter,
}: {
  event: CanonicalEvent | null
  onOpenChange: (open: boolean) => void
  onFilter?: (value: string) => void
}) {
  return (
    <Dialog open={event !== null} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl">
        {event && (
          <>
            <DialogHeader>
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline" className="bg-muted/40 text-[10px] text-foreground/70">{event.category}</Badge>
                <ResultPill result={event.result} />
              </div>
              <DialogTitle className="text-base">{event.action}</DialogTitle>
              <DialogDescription>{formatFull(event.timestamp)} · UTC {event.timestamp.slice(11, 19)}</DialogDescription>
            </DialogHeader>

            <div className="grid gap-2 sm:grid-cols-2">
              <Field icon={<UserRound className="size-3.5" />} label="User" value={event.user} onFilter={onFilter} />
              <Field icon={<Globe2 className="size-3.5" />} label="Source IP" value={event.sourceIp} mono onFilter={onFilter} />
              <Field icon={<Monitor className="size-3.5" />} label="Device" value={event.device} onFilter={onFilter} />
              <Field icon={<Tag className="size-3.5" />} label="Source" value={`${event.sourceName} · ${event.sourceType}`} />
            </div>

            <div className="space-y-1.5">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-medium text-muted-foreground">Raw log line</span>
                <CopyButton value={event.rawContent} label="Copy line" />
              </div>
              <pre className="max-h-48 overflow-auto rounded-lg border border-border bg-background px-3 py-2.5 font-mono text-[11px] leading-5 break-all whitespace-pre-wrap text-foreground">{event.rawContent}</pre>
            </div>

            <DialogFooter className="items-center sm:justify-between">
              <span className="truncate font-mono text-[10px] text-muted-foreground">{event.id}</span>
              <CopyButton value={event.id} label="Copy ID" />
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}

function Field({ icon, label, value, mono = false, onFilter }: { icon: ReactNode; label: string; value: string | null; mono?: boolean; onFilter?: (value: string) => void }) {
  return (
    <div className="rounded-lg border border-border bg-muted/30 px-3 py-2">
      <div className="flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground">{icon}{label}</div>
      <div className="mt-1 flex items-center justify-between gap-2">
        <span className={`truncate text-[12px] ${value ? "text-foreground" : "text-muted-foreground"} ${mono ? "font-mono" : ""}`}>{value ?? "Not present"}</span>
        {value && onFilter && (
          <button type="button" onClick={() => onFilter(value)} className="shrink-0 rounded px-1.5 py-0.5 text-[10px] font-medium text-blue-400 transition hover:bg-blue-500/10 focus-visible:ring-2 focus-visible:ring-blue-500/30 focus-visible:outline-none">
            Filter
          </button>
        )}
      </div>
    </div>
  )
}

function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false)
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1500)
    } catch {
      toast.error("Copy failed", { description: "The browser blocked clipboard access." })
    }
  }
  return (
    <Button variant="ghost" size="sm" onClick={() => void copy()} className="h-7 gap-1.5 text-[11px] text-foreground/70">
      {copied ? <Check className="size-3.5 text-emerald-400" /> : <Copy className="size-3.5" />}
      {copied ? "Copied" : label}
    </Button>
  )
}

function ResultPill({ result }: { result: string | null }) {
  const value = result?.toLowerCase() ?? ""
  const tone = /fail|error/.test(value)
    ? "border-rose-500/30 bg-rose-500/10 text-rose-400"
    : /warn/.test(value)
      ? "border-amber-500/30 bg-amber-500/10 text-amber-400"
      : /success/.test(value)
        ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-400"
        : "border-border bg-muted/40 text-foreground/70"
  return <Badge variant="outline" className={`text-[10px] ${tone}`}>{result ?? "event"}</Badge>
}
