namespace ArquipelagoDoCeu.Generation;

// De onde vem o sorteio das lutas (o do mundo vem da seed). O Godot entrega uma seed nova a cada pedido; os testes, uma fixa.
public interface IRandomSource
{
	Pcg32 NewStream();
}
