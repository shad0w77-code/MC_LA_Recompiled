# Créditos

## Recompilação e base

- **holdmysocks e contribuidores de mcla-recomp** — projeto original de recompilação estática de Midnight Club: Los Angeles Complete Edition: https://github.com/holdmysocks/mcla-recomp.
- **RenanQueiroz e contribuidores de seu fork** — base direta deste repositório, trabalho de desempenho nos renderizadores, launcher Windows e correções: https://github.com/RenanQueiroz/mcla-recomp.
- **ReXGlue e seus contribuidores** — SDK/runtime e ferramentas de recompilação: https://github.com/rexglue/rexglue-sdk.
- **Xenia e seus contribuidores** — trabalho de emulação que fundamenta componentes do runtime. Os avisos correspondentes permanecem nos arquivos e em THIRD-PARTY-NOTICES.md.

O histórico Git da base foi preservado para manter a autoria das contribuições anteriores.

## Este fork

- **Midnight Club L.A. Recompiled** — repositório atual: https://github.com/shad0w77-code/MC_LA_Recompiled. Tradução PT-BR (incluindo correção dos glifos `Õ`/`õ`), integração/validação das melhorias FSR e Frame Generation, suporte de proporção automática/ultrawide e instalador/configurador com atualizador automático. Desenvolvimento com assistência de ferramentas de programação por IA e testes visuais do mantenedor.

Essas melhorias não representam autoria da recompilação original nem dos algoritmos AMD.

## Outros componentes e repositórios de referência

- **mzzvxm / LARecomp** — referência dos pontos de integração e ideias de câmera livre, iluminação/clima, relógio real, fibra de carbono, unidades em km/h, rádio MP3 nas estações do jogo, countdown e checkpoint sweep, conferidos com o executável desta cópia do jogo: https://github.com/mzzvxm/LARecomp.
- **AMD / GPUOpen FidelityFX SDK** — FidelityFX Super Resolution e Frame Generation: https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK e fork ReXGlue https://github.com/rexglue/FidelityFX-SDK. Código aberto e binários mantêm as licenças oficiais correspondentes.
- **Dyotson / ForzaHorizon1-ReImagined** — otimizações Vulkan portadas pela base e wrapper de conquistas (MIT): https://github.com/Dyotson/ForzaHorizon1-ReImagined.
- **Dyotson / RecompiledHaloReach** — wrapper de toast de conquistas (MIT): https://github.com/Dyotson/RecompiledHaloReach.
- **Xenia / Xenia Canary** — base de emulação e shaders de resolve/textura (BSD-3-Clause): https://github.com/xenia-project/xenia e https://github.com/xenia-canary/xenia-canary.
- **madler / zlib (contrib/puff)** — design do descompressor deflate usado em `src/shader_prepare.cpp`: https://github.com/madler/zlib/tree/develop/contrib/puff.
- Demais bibliotecas/autores: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) e licenças dos respectivos projetos.
- **Rockstar Games / Rockstar San Diego** e demais titulares — jogo original, marcas, personagens e conteúdo. Projeto comunitário, sem afiliação ou endosso desses titulares.

A licença GPL-3.0-or-later do fork não substitui as licenças de terceiros nem concede direitos sobre os arquivos do jogo.
