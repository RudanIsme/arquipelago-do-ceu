namespace ArquipelagoDoCeu;

// Falha esperada (dado externo ruim) volta como valor; exceção fica para bug.
public readonly record struct Result<T>(T? Value, string? Error)
{
	public bool IsOk => Error is null;

	public static Result<T> Ok(T value) => new(value, null);

	public static Result<T> Fail(string error) => new(default, error);
}
