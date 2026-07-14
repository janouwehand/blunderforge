import type { MoveRecordDto } from "../api/gameClient";

interface MoveListProps {
  moves: MoveRecordDto[];
  currentPly?: number;
}

export function MoveList({ moves, currentPly }: MoveListProps) {
  if (moves.length === 0) {
    return (
      <div className="move-list" role="region" aria-label="Move list">
        <p className="move-list-empty">No moves played yet.</p>
      </div>
    );
  }

  const pairs: { number: number; white?: MoveRecordDto; black?: MoveRecordDto }[] = [];
  for (let i = 0; i < moves.length; i += 2) {
    pairs.push({
      number: Math.floor(i / 2) + 1,
      white: moves[i].side === "White" ? moves[i] : moves[i + 1],
      black: moves[i].side === "Black" ? moves[i] : moves[i + 1]
    });
  }

  return (
    <div className="move-list" role="region" aria-label="Move list">
      <h3 className="move-list-title">Moves</h3>
      <ol className="move-list-entries" aria-label="Played moves">
        {pairs.map((pair) => (
          <li key={pair.number} className="move-pair">
            <span className="move-number">{pair.number}.</span>
            <span className="move-san" aria-current={pair.white?.ply === currentPly ? "step" : undefined}>{pair.white?.san ?? "..."}</span>
            <span className="move-san" aria-current={pair.black?.ply === currentPly ? "step" : undefined}>{pair.black?.san ?? ""}</span>
          </li>
        ))}
      </ol>
    </div>
  );
}
