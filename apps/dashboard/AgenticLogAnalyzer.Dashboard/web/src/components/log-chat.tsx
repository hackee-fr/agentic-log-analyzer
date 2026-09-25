import { useEffect, useRef, useState, type FormEvent } from "react"
import { Bot, Cpu, FileSearch, LoaderCircle, SendHorizontal, ShieldCheck, UserRound } from "lucide-react"

import { askQuestion, type ChatAnswer } from "@/lib/api"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card } from "@/components/ui/card"
import { Input } from "@/components/ui/input"

type Message =
  | { id: number; role: "user"; text: string }
  | { id: number; role: "assistant"; answer: ChatAnswer }
  | { id: number; role: "error"; text: string }

const suggestions = [
  "Fais-moi un résumé des logs",
  "Y a-t-il des attaques ?",
  "Quelles IP ont échoué ?",
  "Montre les erreurs docker",
  "Chronologie des avertissements",
]

const formatTime = (value: string) =>
  new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", second: "2-digit", timeZone: "UTC" }).format(new Date(value))

export function LogChat() {
  const [messages, setMessages] = useState<Message[]>([])
  const [question, setQuestion] = useState("")
  const [pending, setPending] = useState(false)
  const nextId = useRef(0)
  const bottom = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottom.current?.scrollIntoView({ behavior: "smooth", block: "end" })
  }, [messages, pending])

  const send = async (text: string) => {
    const trimmed = text.trim()
    if (!trimmed || pending) return
    setQuestion("")
    setMessages((current) => [...current, { id: nextId.current++, role: "user", text: trimmed }])
    setPending(true)
    try {
      const answer = await askQuestion(trimmed)
      setMessages((current) => [...current, { id: nextId.current++, role: "assistant", answer }])
    } catch (error) {
      const text = error instanceof Error ? error.message : "Check that the API is running."
      setMessages((current) => [...current, { id: nextId.current++, role: "error", text }])
    } finally {
      setPending(false)
    }
  }

  const onSubmit = (event: FormEvent) => {
    event.preventDefault()
    void send(question)
  }

  return (
    <Card className="flex h-[calc(100vh-260px)] min-h-[520px] flex-col gap-0 overflow-hidden rounded-xl border-border py-0 shadow-[0_1px_2px_rgba(15,23,42,.03)]">
      <div className="flex-1 space-y-5 overflow-y-auto p-5 sm:p-6">
        {messages.length === 0 && (
          <div className="mx-auto flex max-w-xl flex-col items-center pt-10 text-center">
            <div className="grid size-12 place-items-center rounded-2xl bg-blue-500/10 text-blue-400 ring-1 ring-blue-500/20">
              <Bot className="size-6" />
            </div>
            <h2 className="mt-4 text-base font-semibold text-foreground">Ask a question about your logs</h2>
            <p className="mt-1 text-xs leading-5 text-muted-foreground">
              Answers are computed from stored events and deterministic detections. Every answer lists the events used as evidence.
            </p>
            <div className="mt-5 flex flex-wrap justify-center gap-2">
              {suggestions.map((item) => (
                <button key={item} type="button" onClick={() => void send(item)} className="rounded-full border border-border bg-card px-3 py-1.5 text-xs text-foreground/70 transition hover:border-blue-500/40 hover:bg-blue-500/10 hover:text-blue-400">
                  {item}
                </button>
              ))}
            </div>
          </div>
        )}

        {messages.map((message) => {
          if (message.role === "user") {
            return (
              <div key={message.id} className="flex justify-end gap-3">
                <div className="max-w-[80%] rounded-2xl rounded-tr-sm bg-primary px-4 py-2.5 text-sm text-primary-foreground">{message.text}</div>
                <div className="grid size-8 shrink-0 place-items-center rounded-full bg-muted text-muted-foreground"><UserRound className="size-4" /></div>
              </div>
            )
          }
          if (message.role === "error") {
            return (
              <div key={message.id} className="rounded-xl border border-rose-500/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-300">
                {message.text}
              </div>
            )
          }
          return <AssistantMessage key={message.id} answer={message.answer} />
        })}

        {pending && (
          <div className="flex items-center gap-3 text-xs text-muted-foreground">
            <div className="grid size-8 place-items-center rounded-full bg-blue-500/10 text-blue-400"><Bot className="size-4" /></div>
            <LoaderCircle className="size-4 animate-spin text-blue-400" /> Analysing events...
          </div>
        )}
        <div ref={bottom} />
      </div>

      <form onSubmit={onSubmit} className="flex gap-2 border-t border-border bg-muted/30 p-3 sm:p-4">
        <Input
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="Ex. : Quelles erreurs sur la base de données ?"
          aria-label="Question about your logs"
          className="h-10 bg-card text-sm"
          disabled={pending}
        />
        <Button type="submit" disabled={pending || !question.trim()} className="h-10 gap-2 bg-primary px-4 text-xs text-primary-foreground hover:bg-primary/90">
          <SendHorizontal className="size-4" /> Send
        </Button>
      </form>
    </Card>
  )
}

function AssistantMessage({ answer }: { answer: ChatAnswer }) {
  return (
    <div className="flex gap-3">
      <div className="grid size-8 shrink-0 place-items-center rounded-full bg-blue-500/10 text-blue-400"><Bot className="size-4" /></div>
      <div className="min-w-0 max-w-[88%] flex-1 space-y-2">
        <div className="flex flex-wrap items-center gap-1.5">
          {answer.llmUsed ? (
            <Badge variant="outline" className="border-violet-500/30 bg-violet-500/10 text-[10px] text-violet-400"><Cpu className="mr-1 size-3" />LLM rewording · verify with evidence</Badge>
          ) : (
            <Badge variant="outline" className="border-emerald-500/30 bg-emerald-500/10 text-[10px] text-emerald-400"><ShieldCheck className="mr-1 size-3" />Deterministic</Badge>
          )}
          <Badge variant="outline" className="bg-card text-[10px]">{answer.intent}</Badge>
          <Badge variant="outline" className="bg-card text-[10px]">{answer.matchedEventCount} matched events</Badge>
          {answer.appliedFilters.map((filter) => (
            <Badge key={filter} variant="outline" className="bg-card font-mono text-[10px]">{filter}</Badge>
          ))}
        </div>

        <div className="rounded-2xl rounded-tl-sm border border-border bg-card px-4 py-3 text-[13px] leading-6 whitespace-pre-wrap text-foreground">
          {answer.answer}
        </div>

        {answer.llmError && (
          <p className="text-[11px] text-amber-400">LLM answer not used, deterministic answer shown. {answer.llmError}</p>
        )}

        {answer.llmUsed && (
          <details className="rounded-lg border border-border bg-muted/40 px-3 py-2">
            <summary className="cursor-pointer text-[11px] font-medium text-foreground/70">Deterministic facts</summary>
            <pre className="mt-2 text-[11px] leading-5 whitespace-pre-wrap text-foreground/70">{answer.deterministicAnswer}</pre>
          </details>
        )}

        {answer.evidence.length > 0 && (
          <details className="rounded-lg border border-border bg-muted/40 px-3 py-2">
            <summary className="flex cursor-pointer items-center gap-1.5 text-[11px] font-medium text-foreground/70">
              <FileSearch className="size-3.5" /> Evidence ({answer.evidence.length} events)
            </summary>
            <div className="mt-2 max-h-72 space-y-1 overflow-y-auto">
              {answer.evidence.map((item) => (
                <div key={item.id} className="rounded border border-border bg-card px-2 py-1.5 font-mono text-[10px] leading-4 text-foreground/70">
                  <span className="text-muted-foreground">{formatTime(item.timestamp)} · {item.id.slice(0, 8)}</span>
                  <div className="break-all">{item.rawContent}</div>
                </div>
              ))}
            </div>
          </details>
        )}
      </div>
    </div>
  )
}
