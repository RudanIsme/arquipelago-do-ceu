# A voz do sistema nunca é interrompida: o jogo guarda a própria fila

No Windows, interromper uma fala em andamento do TTS do Godot (`TtsStop`, ou `interrupt` no `TtsSpeak`) travou o jogo de vez em quando, e fechar o jogo no meio de uma fala travou o encerramento. O motivo é que o purge da fala espera o motor de áudio, que espera o laço do jogo. A voz nunca pode travar a criança, então o `GodotSpeechEngine` não interrompe mais: guarda o que ainda não foi dito, joga fora o que ficou velho quando chega um aviso novo, só manda falar quando a fala anterior terminou e, ao fechar, espera a fala acabar. O jogo também lista as vozes uma vez só (a lista vazia vale um minuto), porque listar custa cerca de 200 ms.

A alternativa de falar numa thread à parte deixou o laço livre, mas travou ou derrubou o jogo em algumas execuções. O custo da escolha é que um aviso novo espera a fala em andamento acabar (alguns segundos), em vez de cortá-la.
