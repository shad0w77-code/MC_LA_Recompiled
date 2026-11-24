<p align="center"><img src="docs/images/logo.png" alt="Midnight Club Los Angeles — L.A. Recompiled" width="760"></p>

<h1 align="center">Midnight Club L.A. Recompiled</h1>
<p align="center">
  <strong>v1.0.0</strong> • Tradução PT-BR • Ultrawide • AMD FidelityFX & Baixa Latência • Instalador LA Midnight Racing com Atualizador Automático
</p>

<p align="center">
  <a href="https://github.com/shad0w77-code/MC_LA_Recompiled/releases/latest">Baixar v1.0.0 (Windows x64)</a> ·
  <a href="docs/INSTALACAO.md">Instalação detalhada</a> ·
  <a href="docs/CONFIGURACAO.md">Configurações</a> ·
  <a href="docs/MELHORIAS.md">Como foi feito</a> ·
  <a href="docs/COMPILACAO.md">Compilar os fontes</a> ·
  <a href="CREDITS.md">Créditos</a>
</p>

---

Projeto comunitário para jogar **Midnight Club: Los Angeles Complete Edition** nativamente no PC (Windows x64) com melhorias visuais, de desempenho, controles, áudio e tradução completa para **Português do Brasil (PT-BR)**.

A recompilação estática original é de [holdmysocks](https://github.com/holdmysocks/mcla-recomp), sobre a base de [RenanQueiroz](https://github.com/RenanQueiroz/mcla-recomp), com o runtime [ReXGlue](https://github.com/rexglue/rexglue-sdk) e referências funcionais ao [LARecomp](https://github.com/mzzvxm/larecomp). Este projeto preserva integralmente os créditos e licenças desses trabalhos. Não há afiliação com Rockstar Games ou AMD.

---

## Comece aqui (Instalação rápida)

1. Acesse **[Releases](https://github.com/shad0w77-code/MC_LA_Recompiled/releases/latest)** e baixe **`Midnight-Club-LA-Recompiled-Windows-x64.zip`**. *(O arquivo `source.zip` contém apenas o código-fonte.)*
2. Extraia o ZIP para uma pasta estável e abra **`Midnight Club L.A. Recompiled.exe`**.
3. Selecione sua própria **ISO de Midnight Club: Los Angeles Complete Edition (Xbox 360)**.
4. Escolha a pasta de destino do jogo (uma subpasta separada do instalador).
5. Deixe marcada a opção **Traduzir o jogo para português (PT-BR)** *(recomendado)*, ou desmarque para jogar em inglês.
6. Ajuste **Gráficos**, **Controles** e **Rádio e Mundo** como preferir, ou mantenha os padrões iniciais.
7. Clique em **PREPARAR MEU JOGO**. Você pode acompanhar todas as etapas na aba **Registro**.
8. Ao concluir, clique em **▶ JOGAR** ou use o atalho criado na Área de Trabalho.

### Atualizador automático integrado

Ao abrir o instalador/configurador com acesso à internet, ele consulta automaticamente a última release oficial em [`shad0w77-code/MC_LA_Recompiled`](https://github.com/shad0w77-code/MC_LA_Recompiled/releases/latest) (ou manualmente pelo botão **↻ Verificar atualizações**). Quando houver uma versão mais nova:
- Um banner aparece no topo da aba **Início** com o resumo das novidades.
- Clique em **Baixar e atualizar** para baixar o pacote oficial, conferir o **SHA-256**, extrair o novo instalador e atualizar sua instalação **sem precisar da ISO** e **sem perder saves ou configurações**.

> **A ISO e os dados do jogo não são distribuídos.** O assistente extrai e prepara localmente os arquivos da sua própria cópia usando o executável PC já compilado com as melhorias. Mantenha a ISO fora da pasta de destino.

---

## Tudo o que está implementado na v1.0.0

### Instalador, Configurador e Proteção de Saves
- **Visual LA Midnight Racing:** interface WPF escura integrada com barra de título própria, logo e artes embutidas offline, animações suaves de transição entre abas, chips dinâmicos com o resumo da configuração ativa e seções recolhíveis quando o jogo já está instalado.
- **7 abas organizadas:** **Início**, **Gráficos**, **Controles**, **Rádio e Mundo**, **Saves e Mods**, **Registro** e **Créditos**.
- **Backups automáticos ao jogar:** clicar em **▶ JOGAR** gera automaticamente um ZIP dos dados atuais em `save-backups` (excluindo cache de shaders e mantendo as **10 cópias mais recentes**), com recuperação rápida pela aba **Saves e Mods**.
- **Exportação e restauração segura:** validação contra path traversal, staging temporário e preservação dos dados anteriores antes de substituir.
- **Suporte isolado a mods:** seção dedicada **Jogar com Mods** na aba *Saves e Mods* (e script `Jogar-Com-Mods.ps1`) usando pasta separada `userdata-mods` para não arriscar sua campanha principal.

### Tradução PT-BR e Fontes Corrigidas
- **2.815 registros nativos**, **317 linhas de diálogo** e **76 arquivos de legendas** traduzidos (menus, telefone Sidekick, dicas, missões, objetivos, garagem/customização, corridas e avisos).
- **Correção das fontes (`Õ` / `õ`):** patch automático nos recursos de fonte de missões (`mission_font.py`) e do telefone (`phone_font.py`), corrigindo títulos como **`RESUMO DAS MISSÕES`** e mensagens no Sidekick.
- **Tradução opcional:** vem ativada por padrão na preparação, podendo ser desmarcada para manter o jogo em inglês.

### Gráficos, Ultrawide, FSR, Frame Generation e Baixa Latência
- **Proporção Automática e Ultrawide:** suporte a Automático, `16:9` e `21:9` com HUD posicionado nos cantos e correções de proporção para telefone, garagem, painéis, GPS, ícones e cursor móvel.
- **Perfis gráficos unificados:** *Mais desempenho*, *Equilibrado* e *Native AA* sincronizados entre o configurador e o menu do jogo (`config/graphics_profiles.json`).
- **Resolução interna e 1080p nativo:** `720p (1×)`, **`1080p Nativo`**, `1440p (2×)` e `2160p (3×)`, além de limites de FPS estendidos (`30`, `40`, `45`, `50`, `60`, `90`, `120`, `144` ou sem limite).
- **AMD FidelityFX & Baixa Latência:**
  - **FSR 4.1.2** (*Native AA*, *Qualidade*, *Equilibrado*, *Desempenho* + nitidez ajustável) em **DirectX 12** e **Vulkan** em GPUs compatíveis.
  - **Frame Generation 4.0.2** em DirectX 12 e em Vulkan experimental (requer FSR, HDR desligado e reinício).
  - **Baixa Latência (`LOW LATENCY`):** **AMD Anti-Lag 2** e **NVIDIA Reflex** configuráveis no configurador e no menu de pausa.
- **Shaders pré-compilados:** `shaderpack/545407F8.shaderpack` incluído e importado automaticamente (com botão **Preparar shaders** no configurador) para reduzir engasgos na primeira partida.
- **Smooth UI:** interpolação de menus, pop-ups, HUD e câmera do menu de pausa acima de 50 FPS.
- **Controles visuais completos:** HDR (pico, branco de referência, intensidade dos realces), gama PC/TV, filtro anisotrópico (até `16×`), opção **`CAR VINYLS`** (`readback_resolve` direto no menu DISPLAY), sombras, desfoques, densidade da cidade/tráfego, espera de GPU (`mcla_fence_yield`) e bloco único (`mcla_single_tile`).
- **Correções de estabilidade e jogabilidade:** fix de crash no bind de shader dos carros, `checkpoint_sweep` (hangouts, portões e checkpoints acima de 30 FPS), countdown sincronizado em qualquer FPS e `d3d12_msaa_line_guard`.

### Customização, Mundo, Velocímetro e Rádio MP3
- **Peças em Fibra de Carbono:** aba **`CARBON FIBER`** no menu de pausa para aplicar carbono por peça (capô, porta-malas, portas, teto, para-choques, paralamas/saias, spoiler, grade e extras), salvo junto com o veículo.
- **Unidades em `km/h` ou `mph`:** `SPEED UNITS` altera velocímetro, distâncias e placas de limite de velocidade do radar.
- **Mundo e Tempo (`WORLD AND CAMERA`):** escolha de horário fixo, **`REAL CLOCK`** (hora do mundo segue o relógio do PC), **`DAY SPEED`** (`0×` a `10×`) e clima (*Automático*, *Céu limpo*, *Nublado*, *Tempestade*, *Neblina*).
- **Câmera Livre (`F8`) e Captura (`F12`):** câmera livre sem captura forçada do cursor e capturas de tela em PNG salvas na pasta `screenshots/`.
- **Rádio MP3 integrada às estações do jogo:** lê arquivos `.mp3` e subpastas da pasta configurada, organiza pela tag ID3 ou nome da subpasta nas estações originais (`ALL` e gênero) com anúncio opcional na tela (`"TOCANDO: música - artista"`), ou funciona como player independente.

### Controles
- **Teclado e Mouse (`MnK`):** aceleração/freio/direção, navegação de menus, câmera no mouse ou `I/J/K/L`, limpeza de teclas ao perder foco (`Alt+Tab`) e ajuste de sensibilidade.
- **Gamepad:** escolha de ícones (**Xbox**, **PlayStation** ou **Automático**), **Vibração** liga/desliga e **Dead Zones individuais** (0% a 30%) para os analógicos esquerdo e direito.

> Os números FSR identificam os provedores integrados; a seleção efetiva depende da GPU, driver e API e é registrada no log da sessão.

---

## Atalhos úteis dentro do jogo

| Tecla / Atalho | Função |
| --- | --- |
| `W` / `S` / `A` / `D` | Acelerar, frear/ré e esterçar (modo teclado e mouse) |
| `Mouse` ou `I` `J` `K` `L` | Mover câmera |
| `Setas` / `Espaço` / `Backspace` / `Enter` | Navegar menus, confirmar/freio de mão, voltar e pausar |
| `F8` | Ligar/desligar Câmera Livre (Numpad `8/2/4/6/9/3` move, botão direito ou `IJKL` gira, `Shift`/`Ctrl` ajusta velocidade) |
| `F9` / `F10` / `Shift+F10` / `Ctrl+F9..F10` | Controles da Rádio MP3 no modo player separado |
| `F12` | Salvar captura de tela em PNG na pasta `screenshots/` |

---

## Primeira instalação limpa

Nenhum save, nome de jogador do mantenedor ou configuração pessoal acompanha o pacote. O perfil inicial chama-se **`User`**.

| Opção | Valor inicial |
| --- | --- |
| Tela | Tela cheia, tamanho automático |
| Resolução interna | `720p (1×)` |
| Antisserrilhado | `MSAA 2×` |
| Limite / VSync | `30 FPS` / ligado |
| Frame Generation | Desligado |
| Idioma / Proporção / Botões | `PT-BR` (opcional na instalação) / `Automático` / `Xbox` |

---

## Capturas do jogo

Capturas registradas durante o desenvolvimento. Opções gráficas e desempenho variam conforme GPU e configurações escolhidas.

### Jogo em ultrawide
![HUD ajustado aos cantos](docs/images/ultrawide.png)

### GPS
![Mapa e ícones](docs/images/gps.png)

### Telefone em português
![Mensagens e telefone](docs/images/telefone.png)

### Customização
![Melhorias de desempenho traduzidas](docs/images/customizacao.png)

---

## Limitações conhecidas

- Testes principais realizados no Windows (incluindo RX 9060 XT e 3440×1440); outras combinações de GPU/driver ainda precisam de validação.
- Pode ocorrer uma linha verde discreta na borda em alta velocidade com Frame Generation ativado; desligá-lo elimina o artefato.
- Os adesivos (*vinyls*) dos carros mantêm a resolução original da textura montada pelo jogo (512×512).
- Há falas e legendas que ainda podem exigir revisão ao longo da campanha completa (não há dublagem; nomes próprios, marcas e músicas permanecem no original).
- Melhorias ainda não testadas no Linux.

---

## Licença e autoria

O código deste repositório está sob a licença **GPL-3.0-or-later**; componentes de terceiros mantêm suas respectivas licenças. Consulte [LICENSE](LICENSE), [CREDITS.md](CREDITS.md) e [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

O jogo, suas marcas, imagens e demais conteúdos pertencem aos respectivos titulares. Este projeto não concede direitos sobre a ISO nem distribui dados extraídos dela.
