import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Chessboard } from "react-chessboard";
import type { GameStateDto, LegalMoveDto } from "../api/gameClient";

interface ChessBoardViewProps {
  game: GameStateDto;
  legalMoves: LegalMoveDto[];
  onMove: (uci: string) => void;
  disabled: boolean;
  highlightedSquares?: string[];
}

export function ChessBoardView({ game, legalMoves, onMove, disabled, highlightedSquares = [] }: ChessBoardViewProps) {
  const [selectedSquare, setSelectedSquare] = useState<string | null>(null);

  const isPlayerTurn = game.activeSide === game.settings.playerSide;
  const isGameActive = game.status === "Active";
  const canMove = isPlayerTurn && isGameActive && !disabled;

  const boardOrientation = game.settings.playerSide.toLowerCase() as "white" | "black";

  // Clear selection when a move is submitted (disabled transitions to true)
  const wasDisabled = useRef(disabled);
  useEffect(() => {
    if (disabled && !wasDisabled.current) {
      setSelectedSquare(null);
    }
    wasDisabled.current = disabled;
  }, [disabled]);

  // Clear selection when the game changes (new game, takeback, etc.)
  useEffect(() => {
    setSelectedSquare(null);
  }, [game.gameId]);

  // Group legal moves by source square for fast lookup
  const movesByFrom = useCallback(() => {
    const map = new Map<string, LegalMoveDto[]>();
    for (const move of legalMoves) {
      const list = map.get(move.from);
      if (list) {
        list.push(move);
      } else {
        map.set(move.from, [move]);
      }
    }
    return map;
  }, [legalMoves]);

  const movesMap = movesByFrom();
  const legalUciSet = useMemo(() => new Set(legalMoves.map((m) => m.uci)), [legalMoves]);

  // ── Square styles ──────────────────────────────────────────

  const squareStyles: Record<string, React.CSSProperties> = {};

  // Highlight the selected piece's square
  if (selectedSquare) {
    squareStyles[selectedSquare] = {
      background: "rgba(251, 188, 5, 0.7)",
      boxShadow: "inset 0 0 0 3px rgba(185, 128, 0, 0.8)"
    };
  }

  // Show destination circles only for the selected piece
  if (selectedSquare && canMove) {
    const destinations = movesMap.get(selectedSquare);
    if (destinations) {
      for (const move of destinations) {
        squareStyles[move.to] = {
          background: "radial-gradient(circle, rgba(0,0,0,0.25) 28%, transparent 32%)"
        };
      }
    }
  }

  // Coach-highlighted squares take visual precedence
  for (const square of highlightedSquares) {
    squareStyles[square] = {
      background: "rgba(251, 188, 5, 0.55)",
      boxShadow: "inset 0 0 0 3px rgba(185, 128, 0, 0.8)"
    };
  }

  // ── Move lookup helper ─────────────────────────────────────

  const findMove = useCallback(
    (from: string, to: string): string | null => {
      const moves = movesMap.get(from);
      if (!moves) return null;

      // Prefer non-promotion exact match
      const exact = moves.find((m) => m.to === to && !m.promotion);
      if (exact) return exact.uci;

      // Try queen promotion (default for click-to-move)
      const queenPromo = moves.find((m) => m.to === to && m.promotion === "q");
      if (queenPromo) return queenPromo.uci;

      // Fall back to any promotion
      const anyPromo = moves.find((m) => m.to === to && m.promotion !== null);
      if (anyPromo) return anyPromo.uci;

      return null;
    },
    [movesMap]
  );

  // ── Handlers ───────────────────────────────────────────────

  const handleSquareClick = useCallback(
    ({ square }: { piece: { pieceType: string } | null; square: string }) => {
      if (!canMove) return;

      if (selectedSquare !== null) {
        // Klik op hetzelfde veld → deselecteren
        if (square === selectedSquare) {
          setSelectedSquare(null);
          return;
        }

        // Try a move to the clicked square
        const uci = findMove(selectedSquare, square);
        if (uci) {
          setSelectedSquare(null);
          onMove(uci);
          return;
        }
      }

      // Select one of your own pieces on this square, or deselect
      if (movesMap.has(square)) {
        setSelectedSquare(square);
      } else {
        setSelectedSquare(null);
      }
    },
    [canMove, selectedSquare, findMove, onMove, movesMap]
  );

  const handlePieceDrop = useCallback(
    ({ sourceSquare, targetSquare }: { sourceSquare: string; targetSquare: string | null }) => {
      if (!canMove || targetSquare === null || targetSquare === sourceSquare) return false;

      const uci = sourceSquare + targetSquare;
      if (legalUciSet.has(uci)) {
        onMove(uci);
        return true;
      }
      for (const promo of ["q", "r", "b", "n"]) {
        const promoUci = uci + promo;
        if (legalUciSet.has(promoUci)) {
          onMove(promoUci);
          return true;
        }
      }
      return false;
    },
    [canMove, legalUciSet, onMove]
  );

  const handlePieceDragBegin = useCallback(() => {
    setSelectedSquare(null);
  }, []);

  // ── Render ─────────────────────────────────────────────────

  return (
    <div className="chessboard-container" role="region" aria-label="Chessboard">
      <div className="chessboard-wrapper">
        <Chessboard
          options={{
            position: game.currentFen,
            boardOrientation,
            allowDragging: canMove,
            onPieceDrag: handlePieceDragBegin,
            onPieceDrop: handlePieceDrop,
            onSquareClick: handleSquareClick,
            boardStyle: {
              borderRadius: "4px",
              boxShadow: "0 2px 12px rgba(0,0,0,0.15)"
            },
            squareStyles,
            animationDurationInMs: 200
          }}
        />
        {disabled && (
          <div
            className="chessboard-overlay"
            role="status"
            aria-label="Processing..."
          />
        )}
      </div>
    </div>
  );
}
