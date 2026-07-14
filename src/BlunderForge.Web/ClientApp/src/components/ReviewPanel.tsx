import type { GameReviewDto } from "../api/gameClient";
interface Props { review: GameReviewDto | null; busy?: boolean; aiAvailable?: boolean; error?: string; onGenerate?: () => void }
export function ReviewPanel({ review, busy = false, aiAvailable = false, error, onGenerate }: Props) {
  if (busy) return <section className="panel" aria-labelledby="review-title"><h2 id="review-title">Game review</h2><p role="status">AI is generating your game review…</p></section>;
  if (!review) return <section className="panel" aria-labelledby="review-title"><h2 id="review-title">Game review</h2>
    {error && <p role="alert">{error}</p>}
    {aiAvailable && onGenerate
      ? <><p>No review has been generated for this game.</p><button className="btn-secondary" onClick={onGenerate}>Generate AI review</button></>
      : <p>Configure AI Coach and its API key to generate a review.</p>}
  </section>;
  return <section className="panel" aria-labelledby="review-title"><h2 id="review-title">Game review</h2>
    <dl className="review-summary"><div><dt>Result</dt><dd>{review.result}</dd></div><div><dt>Overall quality</dt><dd>{review.overallQuality}</dd></div></dl>
    <h3>What went well</h3><p>{review.wentWell}</p><h3>Future focus</h3><p>{review.futureFocus}</p>
    {review.criticalMoves.length > 0 && <><h3>Critical moves</h3><ol>{review.criticalMoves.map(move => <li key={move.ply}>Move {move.ply}: {move.playedSan} — {move.classification}.</li>)}</ol></>}
  </section>;
}
