export function LinkPrompt() {
  return (
    <div className="center">
      <h2>Аккаунт не привязан</h2>
      <p className="muted">
        Отправь боту в групповом чате клана команду:
      </p>
      <code className="cmd">/link #ТВОЙ_ТЕГ</code>
      <p className="muted">
        Свой тег найдёшь в Clash Royale: профиль → значок под именем.
      </p>
    </div>
  )
}
