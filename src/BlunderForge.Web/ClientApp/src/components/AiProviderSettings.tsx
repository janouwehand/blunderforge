import { useEffect, useState, type FormEvent } from "react";
import { getAiProviderSettings, testAiProvider, updateAiProviderSettings, type AiProviderSettingsDto } from "../api/settingsClient";

export function AiProviderSettings() {
  const [settings, setSettings] = useState<AiProviderSettingsDto | null>(null);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => { getAiProviderSettings().then(setSettings).catch(() => setMessage("Provider settings could not be loaded.")); }, []);
  if (!settings) return <section aria-label="AI provider settings"><p>{message || "Loading provider settings..."}</p></section>;

  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setMessage("");
    try { setSettings(await updateAiProviderSettings({ ...settings!, baseUrl: settings!.baseUrl ?? undefined })); setMessage("Settings saved."); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Save failed."); }
    finally { setBusy(false); }
  }

  async function testConnection() {
    setBusy(true); setMessage("Testing connection...");
    try { const result = await testAiProvider(); setMessage(result.detail); }
    catch (error) { setMessage(error instanceof Error ? error.message : "Connection test failed."); }
    finally { setBusy(false); }
  }

  return <section aria-labelledby="ai-settings-title" className="settings-panel">
    <h2 id="ai-settings-title">Provider settings</h2>
    <p>Secrets are read from the environment or a Docker secret. They are never displayed or edited here.</p>
    <p role="status"><strong>Configuration:</strong> {settings.configured ? "ready for AI explanations" : "incomplete — Stockfish-only coaching remains available"}. Secret: {settings.secretAvailable ? "available" : "unavailable"} ({settings.secretSource}).</p>
    <form onSubmit={save}>
      <label>Provider<select value={settings.provider} onChange={e => setSettings({ ...settings, provider: e.target.value })}><option>DeepSeek</option><option value="OpenAICompatible">OpenAI-compatible</option></select></label>
      <label>Base URL<input type="url" required value={settings.baseUrl ?? ""} onChange={e => setSettings({ ...settings, baseUrl: e.target.value })} /></label>
      <label>Interactive model<input required value={settings.interactiveModel} onChange={e => setSettings({ ...settings, interactiveModel: e.target.value })} /></label>
      <label>Review model<input required value={settings.reviewModel} onChange={e => setSettings({ ...settings, reviewModel: e.target.value })} /></label>
      <label>Timeout (seconds)<input type="number" min="1" value={settings.timeoutSeconds} onChange={e => setSettings({ ...settings, timeoutSeconds: Number(e.target.value) })} /></label>
      <div><button disabled={busy} type="submit">Save</button> <button disabled={busy} type="button" onClick={testConnection}>Test connection</button></div>
      {message && <p role="status">{message}</p>}
    </form>
  </section>;
}
