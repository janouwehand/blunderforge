import type { CoachHelpDto } from "../api/gameClient";

interface Props {
  result: CoachHelpDto | null; disabled: boolean; analyzing: boolean; aiAvailable: boolean; useAi: boolean;
  onUseAiChange: (value: boolean) => void; onCoachMe: () => void; onClose: () => void;
}
export function CoachPanel({ result, disabled, analyzing, aiAvailable, useAi, onUseAiChange, onCoachMe, onClose }: Props) {
  return <section className="coach-panel" aria-labelledby="coach-title">
    <div className="coach-panel-header"><h2 id="coach-title">Coach</h2>{result && <button className="icon-button" aria-label="Close coach result" onClick={onClose}>×</button>}</div>
    <div className="coach-actions">
      <button className="btn-secondary" onClick={onCoachMe} disabled={disabled}>Coach me</button>
      {aiAvailable && <label className="checkbox-label"><input type="checkbox" checked={useAi} onChange={event => onUseAiChange(event.target.checked)} />Use AI explanation and game review</label>}
    </div>
    {analyzing && <p role="status">Stockfish is analyzing the position…</p>}
    {result && <div className="coach-result" role="status">
      <p><strong>{result.textAlternative}</strong></p>
      <p className="muted">Highlighted squares: {result.highlightSquares.join(", ")}. Arrow: {result.arrow.from} to {result.arrow.to}.</p>
      {result.hint && <p><strong>Hint:</strong> {result.hint}</p>}
      {result.explanation && <p>{result.explanation}</p>}
      {result.aiStatus && <p>{result.aiStatus}</p>}
    </div>}
  </section>;
}
