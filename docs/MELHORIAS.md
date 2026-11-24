# Como as melhorias foram feitas

## Base e autoria

O trabalho parte de mcla-recomp e do fork de RenanQueiroz, com ReXGlue. A recompilação estática e as bibliotecas existentes são trabalhos dos autores creditados; as alterações deste projeto não substituem essa autoria.

## Tradução PT-BR

Textos de recursos e strings usadas pelo código foram catalogados e substituídos por traduções. Há 2.815 registros nativos e 317 linhas de diálogo, incluindo 76 recursos de legendas. A preparação aplica as alterações aos dados extraídos da ISO do usuário. Não há redistribuição do arquivo de recursos original neste repositório.

A revisão incluiu acentos, largura dos rótulos, contadores/variáveis, mensagens do telefone, missões e garagem. Nomes próprios e marcas foram preservados. A existência de uma linha traduzida no catálogo não garante que todas as situações da campanha já tenham sido verificadas em tela.

## Proporção e interface

Foram ajustados os caminhos de desenho para preservar tamanho e proporção dos elementos de interface: HUD, telefone, painéis, garagem e GPS. O HUD pode ir aos cantos da saída ultrawide. Ícones do GPS e o cursor móvel receberam correções próprias para evitar deformação e deslocamento da seleção.

O modo automático calcula a proporção a partir da saída/janela na inicialização. O logo original do menu do jogo foi mantido.

## FSR e Frame Generation

A integração fornece modos de reconstrução e seleciona provedores conforme a API e o hardware. O caminho DX12 foi testado com FSR 4.1.2 e FG AMD 4.0.2 por IA. No Vulkan, o SDK 2.4 carrega o provedor de upscaling e registra a versão ativa. A RX 9060 XT selecionou 4.1.2 em 2560×1440. O caminho experimental Vulkan prepara profundidade, movimento e câmera e apresenta um quadro interpolado entre dois reais em SDR.

Os algoritmos e provedores são da AMD; este projeto integra e configura esses componentes.

## Correções e recursos do fork de RenanQueiroz

Em 17/11/2026 foram trazidos para esta árvore, um por vez, itens do fork [RenanQueiroz/mcla-recomp](https://github.com/RenanQueiroz/mcla-recomp), sobre a base já usada aqui. A autoria é dele e dos autores que ele credita (por exemplo, o LARecomp para o countdown e para o checkpoint sweep).

- **Shaders pré-compilados:** o pacote leva `shaderpack/545407F8.shaderpack` (só impressões digitais e descrições de pipeline, sem dados do jogo). O jogo reconstrói os shaders a partir dos arquivos do próprio jogador e o instalador roda `mcla.exe --mcla_prepare_shaders=true` no fim da preparação e da atualização. O jogo também importa o pack a cada início (`mcla_shader_pack_auto`). Um diário e um registro (`shaders/local/*.import` e `*.ledger`) protegem o cache do jogador.
- **SDK (D3D12/Vulkan):** limite do índice relativo de constante (travamento de GPU); proteção de linhas em MSAA na AMD (`d3d12_msaa_line_guard`); backup do save anterior até o jogo fechar o novo; `skip_overwritten_transfers` (desligado por padrão).
- **Latência:** AMD Anti-Lag 2 e NVIDIA Reflex (`low_latency`, linha no menu de pausa). O Anti-Lag 2 está marcado como não testado pelo próprio SDK.
- **Jogo:** correção de crash no bind de shader dos carros; checkpoint sweep (hangouts, portões e checkpoints acima de 30 FPS); countdown em tempo com a corrida em qualquer FPS; vibração e dead zones dos sticks; **Smooth UI** (menus, pop-ups, HUD e câmera do menu de pausa desenhados entre os passos de 30 Hz).
- **Fibra de carbono e hora (ideias do LARecomp):** aba CARBON FIBER no menu de pausa (capô, porta-malas, portas, teto, para-choques, paralamas e saias, spoiler, grill e extras do carro que você dirige; a escolha fica no save, junto com o carro). Em WORLD AND CAMERA, REAL CLOCK (a hora do mundo segue o relógio do PC) e DAY SPEED (de parado a 10x). SPEED UNITS troca o velocímetro e as distâncias de mph para km/h. O rádio MP3 lê também as subpastas, começa numa faixa aleatória e pode colocar as músicas dentro do rádio do jogo (estação ALL e a estação do estilo, pela tag ID3 ou pela subpasta, com "música - artista"), com o som tocado fora do jogo como no LARecomp.

Itens do mesmo fork que **não** foram trazidos: o assistente de espera do Reflex (só NVIDIA), os markers de low latency para quadros gerados no Vulkan e a verificação de carregamento das DLLs da AMD.

## Instalador e configurador

A interface Windows usa WPF e C#. Um pacote interno com manifesto SHA-256 contém runtime e ferramentas de preparação. A ISO é fornecida pelo jogador; a preparação extrai dados, aplica traduções e grava uma instalação com somente os arquivos necessários para jogar e configurar.

Instalação e configuração usam o mesmo executável. O atalho opcional inicia mcla.exe diretamente. Configurações existentes são lidas localmente; arquivos pessoais do mantenedor não fazem parte do pacote.

## Validação

As correções foram desenvolvidas com assistência de ferramentas de programação por IA e conferidas em testes visuais pelo mantenedor, incluindo DX12, Vulkan, ultrawide, pausa, telefone, GPS e garagem. Isso não substitui testes em outras GPUs ou durante toda a campanha.
