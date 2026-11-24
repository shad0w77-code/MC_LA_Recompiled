<p align="center"><img src="docs/images/logo.png" alt="Midnight Club Los Angeles — L.A. Recompiled" width="760"></p>

<h1 align="center">Midnight Club L.A. Recompiled</h1>
<p align="center">PT-BR • Proporção automática e ultrawide • AMD FidelityFX • Instalador e configurador</p>

Projeto comunitário para jogar **Midnight Club: Los Angeles Complete Edition** no PC com melhorias de imagem e interface, mantido por **Araujo**.

A recompilação original é de [holdmysocks](https://github.com/holdmysocks/mcla-recomp). A base direta é o fork de [RenanQueiroz](https://github.com/RenanQueiroz/mcla-recomp), com o runtime [ReXGlue](https://github.com/rexglue/rexglue-sdk). Este projeto preserva os créditos e licenças desses trabalhos. Não há afiliação com Rockstar Games ou AMD.

## Comece aqui

1. Acesse [Releases](https://github.com/shad0w77-code/MC_LA_Recompiled/releases) e baixe **Midnight-Club-LA-Recompiled-Windows-x64.zip** da versão mais recente. O arquivo `source.zip` é só o código-fonte.
2. Extraia o ZIP inteiro para uma pasta estável e abra **Midnight Club L.A. Recompiled.exe**.
3. Selecione sua própria ISO de **Midnight Club: Los Angeles Complete Edition para Xbox 360**.
4. Escolha a pasta do jogo. O padrão é uma subpasta ao lado do instalador; você pode mudar o local e o nome.
5. Deixe marcada a opção **Traduzir o jogo para português (PT-BR)** (padrão), ou desmarque para jogar em inglês.
6. Se quiser, ajuste **Gráficos**, **Controles** e **Rádio e Mundo** antes de preparar. Sem alterações, serão usados os padrões descritos abaixo.
7. Clique em **PREPARAR MEU JOGO**. A aba **Registro** mostra o andamento.
8. Ao terminar, clique em **▶ JOGAR** ou use o atalho da Área de Trabalho.

**Atualizações automáticas:** ao abrir o instalador, ele verifica novas releases em [shad0w77-code/MC_LA_Recompiled](https://github.com/shad0w77-code/MC_LA_Recompiled/releases) e oferece o download verificado (SHA-256) e atualização com um clique — sem precisar da ISO e mantendo saves e configurações.

**A ISO e os dados do jogo não são fornecidos.** O assistente prepara localmente os dados da sua cópia usando a versão PC já compilada com as melhorias. Ele não recompila C++ no computador do jogador. Mantenha a ISO fora da pasta de destino.

[Instalação detalhada](docs/INSTALACAO.md) · [Configurações](docs/CONFIGURACAO.md) · [Como as melhorias foram feitas](docs/MELHORIAS.md) · [Créditos](CREDITS.md) · [Compilar os fontes](docs/COMPILACAO.md)

## O que mudou

| Área | Melhorias |
| --- | --- |
| Idioma | Menus, telefone, dicas, missões (`RESUMO DAS MISSÕES` com `Õ`/`õ` corrigidos), garagem/customização e avisos em PT-BR; legendas traduzidas onde disponíveis. A tradução é opcional na instalação. |
| Ultrawide | Automático, 16:9 e 21:9; HUD nos cantos, correções de proporção de telefone, garagem, painéis, GPS, ícones e cursor. |
| Reconstrução & 1080p | FSR 4.1.2 (IA) em DirectX 12 e Vulkan (FSR, SDR) em GPU compatível; Native AA, Qualidade, Equilibrado, Desempenho, modo 1080p nativo e shaderpack pré-compilado. |
| Frame Generation & Latência | Provedor AMD 4.0.2 por IA em DX12 e Vulkan experimental; baixa latência (AMD Anti-Lag 2 / NVIDIA Reflex) e Smooth UI (interpolação de menus/HUD acima de 50 FPS). |
| Customização, Mundo e Rádio | Fibra de carbono por peça na garagem/pausa, relógio real, velocidade do dia, clima, câmera livre (`F8`), km/h (`SPEED UNITS`) e rádio MP3 integrada às estações do jogo. |
| Instalador e Atualizador | Visual **LA Midnight Racing** integrado, backups automáticos ao jogar (10 cópias), suporte a mods (`userdata-mods`), controles de vibração/dead zone/MnK e atualizador automático via GitHub Releases. |

Os números FSR identificam os provedores integrados. A seleção efetiva depende de GPU, driver e API e pode ser conferida no registro do jogo. Não há promessa de IA em todas as placas.

Escolha **Vulkan** ou **DirectX 12** em *API gráfica* e selecione **FSR** no antisserrilhado. A geração de quadros em Vulkan exige SDR e reinício.

## Primeira instalação limpa

Nenhum save, nome de jogador do mantenedor, histórico ou configuração pessoal acompanha o pacote. O perfil inicial chama-se **User**. Saves novos são criados pelo próprio jogo.

| Opção | Inicial |
| --- | --- |
| Tela | Tela cheia, tamanho automático |
| Resolução interna | 720p (1×) |
| Antisserrilhado | MSAA |
| Limite / VSync | 30 FPS / ligado |
| Sombras e efeitos | Padrões do jogo, sem perfil de desempenho aplicado |
| Frame Generation | Desligado |
| Idioma / proporção / botões | PT-BR (opcional na instalação) / Automático / Xbox |

Esses são os padrões desta distribuição, preservando as correções do projeto. Se você mudar as opções antes de preparar, suas escolhas serão aplicadas. Uma instalação existente pode carregar **os seus próprios** ajustes locais; isso não significa que o pacote inclua o histórico do mantenedor.

## Imagens

Capturas do desenvolvimento; opções e desempenho mostrados não representam os padrões iniciais nem uma garantia de FPS.

### Instalador e configurador
![Instalador e configurador](docs/images/installer.png)

### Jogo em ultrawide
![HUD ajustado aos cantos](docs/images/ultrawide.png)

### GPS
![Mapa e ícones](docs/images/gps.png)

### Telefone em português
![Mensagens e telefone](docs/images/telefone.png)

### Customização
![Melhorias de desempenho traduzidas](docs/images/customizacao.png)

## Limitações conhecidas

- Testes realizados no Windows, incluindo RX 9060 XT e 3440×1440; outras combinações precisam de validação.
- Pode ocorrer uma linha verde discreta na borda em alta velocidade com Frame Generation. Desligá-lo eliminou o artefato nos testes relatados.
- Vulkan foi testado com FSR 4.1.2 em uma RX 9060 XT. Consulte o log da sessão para identificar o provedor na sua GPU.
- Os adesivos (vinyls) dos carros têm a resolução original do jogo, que os monta numa textura de 512×512; de perto, parecem de baixa resolução.
- Há traduções e legendas que ainda exigem conferência durante toda a campanha. Não é dublagem.
- Nomes próprios, marcas e músicas permanecem no original.
- As melhorias não foram testadas no Linux.

## Licença e autoria

A licença do código deste fork é **GPL-3.0-or-later**; componentes de terceiros mantêm suas licenças. Veja [LICENSE](LICENSE), [CREDITS.md](CREDITS.md) e [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Cada distribuição binária deve disponibilizar o código-fonte correspondente e os avisos exigidos. A interface simples para jogadores não altera essas obrigações.

O jogo, suas marcas, imagens e demais conteúdos pertencem aos respectivos titulares. Este projeto não concede direitos sobre a ISO nem distribui os dados extraídos dela.
