interface Props { title: string; message: string; confirmLabel: string; onConfirm: () => void; onCancel: () => void }
export function ConfirmDialog({ title, message, confirmLabel, onConfirm, onCancel }: Props) {
  return <div className="dialog-backdrop"><section className="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-title" aria-describedby="confirm-message">
    <h2 id="confirm-title">{title}</h2><p id="confirm-message">{message}</p>
    <div className="button-row"><button autoFocus className="btn-danger" onClick={onConfirm}>{confirmLabel}</button><button className="btn-secondary" onClick={onCancel}>Cancel</button></div>
  </section></div>;
}
