using System.Linq;
using Godot;

namespace ArquipelagoDoCeu;

public partial class Main
{
	void InstallMeshyTest()
	{
		if (!MeshyAssets.Enabled || OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--driver="))) return;
		if (current is null && profiles.Profiles.Count == 0)
		{
			var created = profiles.Create("Teste de assets");
			if (created.IsOk) Enter(created.Value!);
		}
		var gate = new MeshyTestGate { Game = this };
		GetTree().Root.AddChild(gate);
		GD.Print("MESHY_TEST pronto: F7 catálogo; save de teste; apresentação normal sem --meshy-test.");
	}

	public void OpenMeshyTower(string discipline)
	{
		if (current is null) return;
		if (inDungeon) LeaveDungeon("");
		EnterDungeon(discipline);
	}
}
