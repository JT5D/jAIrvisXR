/**
 * Typed HTTP client for Jarvis Daemon API.
 */
import type { DaemonStatus, CommandResult, ContextItem, LogEntry } from "./types";

export class JarvisClient {
  private endpoint: string;

  constructor(endpoint: string) {
    this.endpoint = endpoint.replace(/\/$/, "");
  }

  private url(path: string): string {
    return `${this.endpoint}${path}`;
  }

  async health(): Promise<DaemonStatus> {
    const res = await fetch(this.url("/api/health"));
    if (!res.ok) throw new Error(`Health check failed: ${res.status}`);
    return res.json();
  }

  async status(): Promise<Record<string, unknown>> {
    const res = await fetch(this.url("/api/status"));
    if (!res.ok) throw new Error(`Status failed: ${res.status}`);
    return res.json();
  }

  async sendCommand(command: string): Promise<CommandResult> {
    const res = await fetch(this.url("/api/command"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ command }),
    });
    if (!res.ok) throw new Error(`Command failed: ${res.status}`);
    return res.json();
  }

  async shareContext(
    type: string,
    content: string,
    meta?: Record<string, unknown>
  ): Promise<{ status: string }> {
    const res = await fetch(this.url("/api/share"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ type, content, meta }),
    });
    if (!res.ok) throw new Error(`Share failed: ${res.status}`);
    return res.json();
  }

  async getContext(): Promise<ContextItem[]> {
    const res = await fetch(this.url("/api/context"));
    if (!res.ok) throw new Error(`Context failed: ${res.status}`);
    return res.json();
  }

  async clearContext(): Promise<void> {
    const res = await fetch(this.url("/api/context"), { method: "DELETE" });
    if (!res.ok) throw new Error(`Clear failed: ${res.status}`);
  }

  async getLogs(count = 20, agent?: string): Promise<LogEntry[]> {
    const params = new URLSearchParams({ n: String(count) });
    if (agent) params.set("agent", agent);
    const res = await fetch(this.url(`/api/logs?${params}`));
    if (!res.ok) throw new Error(`Logs failed: ${res.status}`);
    return res.json();
  }

  async isReachable(): Promise<boolean> {
    try {
      await this.health();
      return true;
    } catch {
      return false;
    }
  }
}
