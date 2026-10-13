namespace ArquipelagoDoCeu.Saves;

// O save em uso. Perfis e configurações gravam no mesmo arquivo, então os dois passam por aqui para
// um nunca apagar a mudança do outro; a mudança só vale depois de gravada.
public sealed class SaveSession(SaveStore store, SaveData data)
{
	public SaveData Data { get; private set; } = data;

	public Result<SaveData> Commit(Func<SaveData, SaveData> change)
	{
		var next = change(Data);
		var written = store.Write(next);
		if (written.IsOk) Data = next;
		return written;
	}
}
