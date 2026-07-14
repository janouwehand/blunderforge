export interface GameSettingsDto { playerColorChoice: string; playerSide: string; opponentElo: number }
export interface MoveRecordDto { ply: number; side: string; san: string; uci: string; fenBefore: string; fenAfter: string }
export interface GameStateDto {
  gameId: string; settings: GameSettingsDto; status: string; result: string; terminationReason: string;
  activeSide: string; currentFen: string; moves: MoveRecordDto[]; whiteKingInCheck: boolean; blackKingInCheck: boolean;
}
export interface StartGameRequestDto { playerColorChoice: string; opponentElo: number }
export interface MoveResultDto { move: MoveRecordDto; state: GameStateDto; opponentMovePending: boolean }
export interface LegalMoveDto { uci: string; from: string; to: string; promotion: string | null }
export interface LegalMovesResponseDto { currentFen: string; moves: LegalMoveDto[] }
export interface CoachHelpDto {
  recommendedMove: string; recommendedMoveUci: string; textAlternative: string; highlightSquares: string[];
  arrow: { from: string; to: string }; hint: string | null; explanation: string | null; aiStatus: string | null;
}
export interface CriticalPositionDto { ply: number; fen: string; playedSan: string; playedUci: string; bestMoveUci: string | null; classification: string; centipawnLoss: number }
export interface GameReviewDto { gameId: string; result: string; overallQuality: string; criticalMoves: CriticalPositionDto[]; wentWell: string; futureFocus: string; usedAi: boolean }
export interface GameHistoryItemDto { gameId: string; date: string; result: string; playerColor: string; opponentElo: number }
export interface GameHistoryPageDto { items: GameHistoryItemDto[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export interface HistoricalGameDto {
  gameId: string; startedAt: string; completedAt: string | null; status: string; result: string; playerColor: string;
  opponentElo: number; initialFen: string; moves: MoveRecordDto[]; review: GameReviewDto | null;
}
interface ErrorResponse { detail?: string }

async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  if (!response.ok) {
    const error = await response.json().catch(() => ({})) as ErrorResponse;
    throw new Error(error.detail ?? `Request failed (${response.status}).`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}
const json = (body: unknown): RequestInit => ({ method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });

export const getActiveGame = (signal?: AbortSignal) => apiFetch<GameStateDto>("/api/games/active", { signal });
export const startGame = (request: StartGameRequestDto) => apiFetch<GameStateDto>("/api/games", json(request));
export const submitMove = (uci: string) => apiFetch<MoveResultDto>("/api/games/active/moves", json({ uci }));
export const completeOpponentTurn = () => apiFetch<MoveResultDto>("/api/games/active/opponent-turn", { method: "POST" });
export const getLegalMoves = (signal?: AbortSignal) => apiFetch<LegalMovesResponseDto>("/api/games/active/moves/legal", { signal });
export const requestCoachHelp = (useAiExplanation: boolean) => apiFetch<CoachHelpDto>("/api/games/active/coach", json({ useAiExplanation }));
export const takeBack = () => apiFetch<GameStateDto>("/api/games/active/takeback", { method: "POST" });
export const resignGame = (side: string) => apiFetch<GameStateDto>("/api/games/active/resign", json({ side }));
export const deleteActiveGame = () => apiFetch<void>("/api/games/active", { method: "DELETE" });
export const getGameHistory = (page: number, signal?: AbortSignal) => apiFetch<GameHistoryPageDto>(`/api/games?page=${page}`, { signal });
export const getHistoricalGame = (gameId: string, signal?: AbortSignal) => apiFetch<HistoricalGameDto>(`/api/games/${gameId}`, { signal });
export const generateGameReview = (gameId: string) => apiFetch<GameReviewDto>(`/api/games/${gameId}/review`, { method: "POST" });
export const deleteHistoricalGame = (gameId: string) => apiFetch<void>(`/api/games/${gameId}`, { method: "DELETE" });
