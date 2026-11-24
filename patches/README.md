# Patches do SDK

Use `scripts/prepare_sdk.ps1`. O patch consolidado `rexglue-release.patch` aplica-se ao commit registrado em `dependencies.json` e reúne todas as alterações atuais do SDK local (incluindo correções de teclado/mouse, FSR e Frame Generation em Vulkan, HUD de métricas, baixa latência Anti-Lag 2/Reflex e ajustes D3D12/Vulkan). Os patches incrementais antigos da base foram absorvidos nesta árvore; a autoria e o histórico continuam preservados no Git. A alteração local de FFmpeg é aplicada separadamente via `FFmpeg.patch`. Os links do libmspack são expandidos no arquivo de fontes, sem mudar seu código.

A aplicação do patch e a revisão do código não substituem o teste em corrida.
