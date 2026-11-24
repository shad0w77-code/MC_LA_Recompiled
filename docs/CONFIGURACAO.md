# Configuração

O configurador está no mesmo executável do instalador. Para uma instalação reconhecida, ele carrega as opções locais; você não precisa preparar novamente a ISO para mudar gráficos.

## Gráficos

- **Resolução de renderização:** a escala inteira padrão parte de `1× = 720p` e vai para `2× = 1440p` e `3× = 2160p` de referência vertical (a largura acompanha a proporção). Há também o modo experimental **1080p** (guest `1920×1080` em escala `1×`, travado em FSR Native AA e proporção 16:9). É diferente do tamanho da janela/saída do monitor.
- **Proporção automática:** usa a tela em fullscreen ou a área da janela na inicialização. Também há 16:9 e 21:9 manuais.
- **API gráfica.** Automático deixa o runtime escolher. DirectX 12 e Vulkan oferecem FSR 4.1.2 em hardware compatível. Reinicie após trocar a API e procure “FSR provider active” no log.
- **MSAA:** padrão inicial. Para usar os modos FSR, selecione FSR no antisserrilhado.
- **Menus gráficos:** **Render** reúne perfil, resolução, API, filtro anisotrópico e vinis. **FSR e quadros** reúne antisserrilhado, modo FSR, nitidez e geração de quadros. **Efeitos** contém os três desfoques; **Sombras** contém os ajustes de sombras e árvores. HDR tem página própria. As páginas menores e os rótulos compactos evitam cortes no espaço do menu original. O menu salva os ajustes ao sair. **(R)** exige reiniciar; **(R!)** indica alteração pendente para a próxima abertura.
- **Filtro anisotrópico:** Jogo preserva o comportamento original dos samplers; Desligado e 1× a 16× forçam o nível para texturas compatíveis. O ReXGlue aceita mudanças durante o jogo, mas texturas/samplers já criados podem demorar a refletir o novo valor. O padrão do SDK é 4×.
- **Opções do configurador:** o painel Gráficos também permite escolher o filtro anisotrópico, os vinis dos veículos, FPS de 40/45/50/90 além das taxas usuais, brilho máximo e branco de referência HDR, intensidade dos realces, frequência do HUD, distância do tráfego, otimização de espera da GPU, renderização em bloco único quando suportada e a abertura normal/rápida/sem logos. Essas opções são persistidas no `mcla.toml`.
- **Native AA:** sem redução de resolução pelo FSR. Qualidade, Equilibrado e Desempenho reduzem a resolução usada na reconstrução, com diferentes compromissos de imagem e custo.
- **Frame Generation.** No Vulkan experimental, use antisserrilhado FSR e HDR desligado e reinicie. O caminho Vulkan interpola a imagem com HUD. O log registra o provedor e o número dos quadros intermediários enviados. O modo por IA requer hardware compatível. DirectX 12 continua disponível.
- **VSync e FPS:** comece com os padrões. Ajuste conforme a tela e a estabilidade desejada.
- **Sombras, efeitos, detalhe e densidade:** os perfis são atalhos opcionais; não são aplicados sem sua escolha.

Os perfis do jogo e do configurador usam a mesma tabela (`config/graphics_profiles.json`). **Mais desempenho** usa 720p base, FSR Qualidade, nitidez 0,2 e reduz sombras/detalhes. **Equilibrado** mantém 720p base, FSR Qualidade e detalhes em 75%. **Native AA** usa 720p base sem redução pelo FSR e detalhes completos. Eles preservam sua API, HDR, Frame Generation, clima e horário. Resolução maior pode melhorar a imagem, mas aumenta o custo; não há promessa de ganho de FPS sem medir o percurso na sua GPU. Nitidez excessiva pode produzir halos.

## Mundo e câmera

Os atalhos de diagnóstico do runtime **F3**, **F4** e **aspas/backtick** estão desativados nesta versão local. Os ajustes continuam no menu de pausa do jogo e no configurador; F8 (câmera) e F12 (captura) permanecem disponíveis.

Na pausa, abra **Mundo e câmera**. Horário e clima também estão no configurador. Escolha **Automático** para devolver o controle ao jogo; desmarque **Fixar horário escolhido** para o ciclo continuar após o horário selecionado. Climas disponíveis: céu limpo, nublado, tempestade e neblina. Esses controles mudam o mundo ao vivo e podem interferir na ambientação de missões; prefira Automático para jogar a campanha normalmente.

**F8** liga/desliga a câmera livre durante a partida. A câmera começa na posição da câmera atual e volta ao controle do jogo ao desligar. Com **Num Lock ligado**, use **8/2/4/6 do teclado numérico** para mover, **9/3** para subir/descer, **I/J/K/L** ou arraste segurando o botão direito do mouse para olhar. **Shift** acelera e **Ctrl** desacelera. Os controles de direção continuam ativos: estacione para fotografar. A câmera não prende o cursor; ao perder foco ela para de receber movimento. Sua ativação é temporária e não é gravada pelo menu para a próxima abertura.

Foram conferidos em jogo, com dados de teste isolados: troca de 12h/céu limpo para 0h/tempestade, ativação/desativação da câmera e retorno ao modo automático. Corridas, transições de garagem e campanha completa ainda exigem validação prolongada.

Clique em **Salvar configurações** e reinicie o jogo para aplicar. As opções do menu do próprio jogo continuam disponíveis.

O renderizador nativo Plume, MSAA 4×/8×, FXAA e SMAA presentes em outras recomps dependem de uma implementação gráfica própria. O menu atual expõe somente opções com suporte no renderizador instalado.

## Capturas de tela

No Windows, pressione **F12** durante o jogo para salvar a saída gráfica em PNG na subpasta `screenshots` da instalação, com data e hora no nome. O recurso pode ser desligado no configurador, na aba **Controles** do menu de pausa ou com `mcla_screenshot_hotkey = false` no `mcla.toml`. O configurador oferece o botão **Abrir capturas de tela**. A captura utiliza a resolução da saída disponibilizada pelo presenter do ReXGlue, e pode causar uma breve pausa ao ler a imagem da GPU. Se o presenter ainda não tiver uma imagem do jogo, a tentativa é registrada no log.

## Controles, Rádio e Mundo

O nome inicial é **User**. Escolha até 15 caracteres ASCII aceitos pelo campo. Alterar o nome pode selecionar outra pasta de perfil; os saves antigos não são apagados.

Os ícones começam como Xbox. Você pode selecionar PlayStation ou Automático. A escolha muda as indicações visuais e não substitui a configuração física do controle.

A aba **Controles** também permite ativar teclado e mouse (`MnK`), ajustar a sensibilidade do mouse, ligar/desligar a **vibração** e definir a **dead zone** dos analógicos esquerdo e direito. As opções de áudio e ambiente ficam na aba **Rádio e Mundo** do configurador (e em **Controles**, **Mundo e câmera** e **Rádio MP3** no menu de pausa do jogo).

- **Dirigir:** W acelera, S freia ou dá ré, A e D esterçam; as setas navegam pelos menus. Espaço, Enter e Backspace atendem às ações comuns do controle. A câmera usa o mouse quando habilitado; I/J/K/L são alternativas pelo teclado. Os atalhos dependem do contexto do jogo e podem ser alterados em `mcla.toml`.
- **Rádio MP3:** coloque arquivos `.mp3` na pasta `radio` ao lado do executável, ou escolha outra pasta no configurador. Ative-a em **Rádio e Mundo** no configurador ou em **Rádio MP3** no menu de pausa. **F9** pausa/retoma, **F10** avança, **Shift+F10** volta; **Ctrl+F9/F10** baixa/aumenta o volume no player separado. No menu de pausa há volume, anterior/próxima, identificação da faixa e **Reler pasta**, sem reiniciar. Com **Inserir MP3s nas estações do jogo** ativado, as músicas entram na estação ALL e nas estações por gênero (ID3/subpasta). Pasta vazia ou sem arquivos reproduzíveis não silencia a música nativa. O recurso inicia desligado.

O uso de teclado/mouse e rádio MP3 requer a versão do jogo com esses recursos compilados. A navegação e a resposta dos controles devem ser validadas dentro de uma corrida, além de sua presença nos menus.

## Latência, interface e controles (itens novos)

| Opção no menu de pausa | Valor no `mcla.toml` | Observação |
| --- | --- | --- |
| LOW LATENCY (aparece como AMD ANTI LAG 2 ou NVIDIA REFLEX) | `low_latency` = `auto`, `off` ou `on` | `auto` liga só o Reflex; o Anti-Lag 2 fica desligado até você ligar. Indisponível se a GPU ou o driver não suportam. |
| SMOOTH UI | `mcla_ui_interpolate` = `true` ou `false` | Desenha menus, HUD e a câmera do menu de pausa entre os passos de 30 Hz. Sem efeito abaixo de 50 FPS; o desenho fica até 33 ms atrás do jogo. |
| VIBRATION | `mcla_vibration` | Liga ou desliga a vibração dos controles. |
| LEFT / RIGHT STICK DEAD ZONE | `mcla_left_stick_deadzone`, `mcla_right_stick_deadzone` | Em porcentagem (OFF a 30%), para analógico com drift. |
| REAL CLOCK | `mcla_time_realtime` | A hora do mundo segue o relógio do PC. |
| DAY SPEED | `mcla_time_speed` | Velocidade do ciclo dia e noite (0 para, 1 normal, até 10). Vale com a hora automática. |
| CARBON FIBER (aba) | guardado no carro, não no `mcla.toml` | Escolha as peças em carbono. Só fica ativa depois que o seu carro foi desenhado uma vez. |
| Botão "Preparar shaders" (aba Início do configurador) | executa `mcla.exe --mcla_prepare_shaders=true` | Reconstrói os shaders a partir dos arquivos do seu jogo e importa o pacote. Útil depois de trocar de placa de vídeo ou de driver. O jogo precisa estar fechado. |
| SPEED UNITS (WORLD AND CAMERA) | `mcla_speed_units` = `mph` ou `kmh` | Velocímetro, distâncias e a placa de limite de velocidade do radar. |
| MP3S IN THE GAME RADIO (MP3 RADIO) | `mcla_radio_in_game` = `true` ou `false` | As músicas da pasta do rádio MP3 (com subpastas) entram no rádio do jogo: na estação ALL e na estação do estilo (o gênero da tag ID3 ou, sem ela, o nome da subpasta; o que não se encaixa vai para ECLECTIC), com o nome "música - artista" das tags. São escolhidas pelo menu de rádio e pelo direcional como as originais, e podem ser adicionadas à CUSTOM PLAYLIST pelo menu. Vale ao reiniciar o jogo. Ligada, o player separado (F9/F10) fica desligado. O volume segue o volume de música do jogo vezes MUSIC VOLUME. |
| SHOW SONG NAME (MP3 RADIO) | `mcla_radio_announce` | Mostra "TOCANDO: nome" na faixa abaixo do velocímetro quando a música muda. |

Sem linha de menu: `skip_overwritten_transfers` (desligado), `d3d12_msaa_line_guard` (ligado), `mcla_ui_countdown_fix` (ligado), `mcla_shader_pack_auto` (ligado). Para repetir a preparação dos shaders depois de trocar de driver, rode `mcla.exe --mcla_prepare_shaders=true`.

## Saves e Mods

Os dados pertencem à instalação do jogador. A aba mostra o caminho, permite exportar um ZIP e **restaurar um ZIP exportado pelo próprio configurador** com o jogo fechado. Na restauração, os caminhos internos são validados para bloquear travessia de diretórios, duplicação e conflitos entre pastas e arquivos. O total extraído é limitado a 10 GB e 20.000 entradas. Os arquivos são extraídos primeiro para uma pasta temporária ao lado dos dados, e os dados existentes são movidos para uma pasta de segurança (`.antes-da-restauracao-*`) antes da troca. A confirmação mostra o total de entradas e tamanho descompactado. A importação de saves do Xenia/RPCS3 ainda requer conversão específica. A restauração precisa de espaço livre suficiente para manter temporariamente os dados originais e os restaurados.

Os backups das configurações ficam em `config-backups`; não acompanham o pacote distribuído.

Ao iniciar pelo botão **JOGAR** do configurador, os dados atuais são copiados para um ZIP em `save-backups`, antes de abrir o jogo. São mantidas as **10 cópias automáticas mais recentes**. A primeira abertura sem saves não cria um ZIP vazio; o cache de shaders não é incluído. Se o backup falhar, o jogo não é iniciado e o configurador mostra o erro. Backups manuais e pastas de segurança de restauração não entram nessa limpeza. Use **Abrir backups automáticos** para localizar os ZIPs e **Restaurar ZIP** para recuperá-los. Abrir `mcla.exe` ou o atalho direto do jogo não aciona o backup automático.

O configurador e o menu do jogo usam o mesmo `mcla.toml`. O configurador preserva valores personalizados e grava apenas os campos alterados, mantendo mudanças que o jogo tenha salvo depois da abertura da interface. Para reler os valores sem alterar nada, clique em **Salvar configurações**. As opções marcadas **requer reiniciar** correspondem às opções de inicialização do runtime; no jogo elas mostram **próxima abertura** e, depois de alteradas, **reinício necessário**. No configurador, todas as alterações são aplicadas na próxima abertura e o jogo precisa estar fechado para salvar.

O botão **Recuperar backup automático** abre a pasta das cópias com o ZIP mais recente legível selecionado. O configurador tenta ler todas as entradas antes de sugeri-lo; se o ZIP estiver incompleto ou inacessível, registra o problema e tenta a cópia anterior. A restauração ainda pede confirmação e preserva a pasta atual antes da troca.

## Jogar com mods

Na aba **Saves e Mods** do configurador, selecione uma cópia da pasta `game` com as modificações aplicadas e clique em **Iniciar com Mods**. Você também pode executar `./Jogar-Com-Mods.ps1 "D:\Jogos\MCLA modificado"` no PowerShell da instalação. O jogo usa os dados escolhidos e guarda o progresso das sessões com mods separadamente em `userdata-mods`. Para voltar à campanha principal, abra o jogo pelo atalho habitual ou pelo botão **JOGAR**.
