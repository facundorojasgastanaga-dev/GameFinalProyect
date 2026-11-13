# Música IRON REQUIEM

Proyectos LMMS 1.2.2 editables, generados por script y renderizados a `Assets/IronRequiem/Audio/Music/`.

- SoundFont: GeneralUser GS 2.0.3 en `Tools/IronRequiem/Downloads/SoundFonts/` (ignorado por git; licencia permite uso comercial, ver `LICENSE.txt`). Los `.mmp` lo referencian con ruta absoluta.
- `Combat01/IR_Combat01_Full.mmp`: intro + riff galopante + breakdown con doble bombo + blast beats con lead (180 BPM, drop C/frigio, 28 compases, 37 s).
- `Combat01/IR_Combat01_Loop.mmp`: igual sin intro, región de loop exacta de 24 compases (32 s) para reproducir en bucle.
- `Menu01/IR_Menu01_Loop.mmp` (`generate_menu01.mjs`): tema de menú "Requiem for Steel", 80 BPM, Do menor (i–VI–iv–V | i–VI–bII–V), 16 compases (48 s) en bucle. Mitad calma (coro, cuerdas, campanas, piano) y mitad pesada (guitarras, timbales, batería a medio tiempo, lead).
- Guitarras, bajo y batería llevan Waveshaper, Dynamics Processor y Bass Booster nativos de LMMS (editables en la cadena FX de cada pista).
- Renderizar siempre con `-l`: sin él, LMMS 1.2.2 dio una vez la mezcla completa ~8 dB más fuerte y saturada. Verificar con `wav_levels.mjs` (objetivo: pico < -1 dB, 0 clipped).

Regenerar y renderizar (sobrescribe los `.mmp`; si los editaste a mano en LMMS, renderiza solo):

```bash
node generate_combat01.mjs ../Downloads/SoundFonts/GeneralUser-GS.sf2 ./Combat01
"C:/Program Files/LMMS/lmms.exe" render Combat01/IR_Combat01_Loop.mmp -f wav -l -i sincmedium -o ../../../Assets/IronRequiem/Audio/Music/IR_Combat01_Loop.wav
node wav_levels.mjs ../../../Assets/IronRequiem/Audio/Music/IR_Combat01_Loop.wav
node generate_menu01.mjs ../Downloads/SoundFonts/GeneralUser-GS.sf2 ./Menu01
"C:/Program Files/LMMS/lmms.exe" render Menu01/IR_Menu01_Loop.mmp -f wav -l -i sincmedium -o ../../../Assets/IronRequiem/Audio/Music/IR_Menu01_Loop.wav
```

En Unity, para música: Load Type `Streaming`, Compression `Vorbis`.
