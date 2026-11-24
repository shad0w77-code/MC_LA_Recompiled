// Generated from localization/pt-BR/host-menu-texts.json. UTF-8 input for the Flash text API.
#pragma once
inline std::string MclaLocalizeMenuText(std::string text) {
  if (!REXCVAR_GET(mcla_ptbr)) return text;
  struct Pair { const char* en; const char* pt; };
  static const Pair pairs[] = {
    {"LOADING", "CARREGANDO"},
    {"SAVE GAME", "SALVAR JOGO"},
    {"YES", "SIM"},
    {"NO", "N\303\203O"},
    {"ON", "SIM"},
    {"OFF", "N\303\203O"},
    {"DISPLAY", "V\303\215DEO"},
    {"RENDER", "RENDERIZA\303\207\303\203O"},
    {"PERFORMANCE", "DESEMPENHO"},
    {"CONTROLS", "CONTROLES"},
    {"KEYBOARD CONTROLS", "TECLADO"},
    {"MOUSE CAMERA", "C\303\202MERA MOUSE"},
    {"MOUSE SENSITIVITY", "SENSIB. MOUSE"},
    {"LOCAL MP3 RADIO", "R\303\201DIO MP3 LOCAL"},
    {"FPS TARGET", "LIMITE DE FPS"},
    {"HUD UPDATES", "TAXA DO HUD"},
    {"UNCAPPED", "SEM LIMITE"},
    {"EVERY FRAME", "POR QUADRO"},
    {"VSYNC", "VSYNC"},
    {"FULLSCREEN", "TELA CHEIA"},
    {"RESOLUTION", "RESOLU\303\207\303\203O"},
    {"ASPECT RATIO", "PROPOR\303\207\303\203O"},
    {"GRAPHICS API", "API GR\303\201FICA"},
    {"DISPLAY GAMMA", "GAMA DA TELA"},
    {"PEAK BRIGHTNESS", "BRILHO M\303\201XIMO"},
    {"PAPER WHITE", "BRANCO HDR"},
    {"HIGHLIGHTS", "REALCES"},
    {"MOTION BLUR", "DESF. MOVIMENTO"},
    {"DEPTH OF FIELD", "PROF. DE CAMPO"},
    {"SINGLE TILE RENDERING", "BLOCO \303\232NICO"},
    {"GPU WAIT YIELD", "ESPERA DA GPU"},
    {"SHADOWS", "SOMBRAS"},
    {"FOLIAGE SHADOWS", "SOMBRA FOLHAS"},
    {"CITY DETAIL DISTANCE", "DETALHE CIDADE"},
    {"TRAFFIC DETAIL DISTANCE", "DETALHE TR\303\201FEGO"},
    {"TRAFFIC DISTANCE", "DIST. TR\303\201FEGO"},
    {"PEDESTRIANS", "PEDESTRES"},
    {"PARKED CARS", "ESTACIONADOS"},
    {"RACE SHADOWS", "SOMBRA CORRIDA"},
    {"SIMPLE CAR SHADOWS", "SOMBRA SIMPLES"},
    {"DISTANT TREES", "\303\201RVORES LONGE"},
    {"FULLSCREEN BLUR", "DESF. TELA"},
    {"ANTI ALIASING", "ANTISSERRILHADO"},
    {"ANISOTROPIC FILTER", "FILTRO ANISO"},
    {"FSR MODE", "MODO FSR"},
    {"FRAME GENERATION", "GERAR QUADROS"},
    {"FSR SHARPNESS", "NITIDEZ DO FSR"},
    {"BUTTON PROMPTS", "\303\215CONES"},
    {"INTRO", "INTRODU\303\207\303\203O"},
    {"HIDE MOUSE POINTER", "OCULTAR CURSOR"},
    {"SCREENSHOT F12", "CAPTURA F12"},
    {"RESTART GAME", "REINICIAR JOGO"},
    {"QUIT GAME", "SAIR DO JOGO"},
    {"NEXT START", "REINICIAR"},
    {"RESTART NEEDED", "REINICIE"},
    {"UNAVAILABLE", "SEM SUPORTE"},
    {"NOT AVAILABLE", "SEM SUPORTE"},
    {"DX12 ONLY", "DX12"},
    {"NATIVE (720P)", "NATIVO (720P)"},
    {"AUTO", "AUTO"},
    {"NORMAL", "NORMAL"},
    {"FAST", "R\303\201PIDA"},
    {"SKIP (NO LOGO)", "PULAR (SEM LOGO)"},
    {"PC MONITOR", "MONITOR"},
    {"BRIGHT", "BRILHANTE"},
    {"BRIGHTER", "MAIS BRILHANTE"},
    {"BRIGHTEST", "BRILHO INTENSO"},
    {"NATIVE AA", "AA NATIVO"},
    {"GAME", "JOGO"},
    {"QUALITY", "QUALIDADE"},
    {"BALANCED", "EQUILIBRADO"},
    {"NONE", "NENHUMA"},
    {"AUTOMATIC", "AUTO"},
    {"PRESS AGAIN", "PRESSIONE NOVAMENTE"},
    {"EFFECTS AND VINYLS", "EFEITOS E VINIS"},
    {"QUALITY PROFILE", "PERFIL"},
    {"CUSTOM", "PERSONALIZADO"},
    {"SOME NEXT START", "REINICIAR"},
    {"FSR OFF", "SEM FSR"},
    {"WORLD AND CAMERA", "MUNDO E C\303\202MERA"},
    {"FREE CAMERA F8", "C\303\202MERA LIVRE (F8)"},
    {"CAMERA SPEED", "VELOCIDADE"},
    {"CAMERA LOOK SPEED", "ROTA\303\207\303\203O"},
    {"WORLD HOUR", "HOR\303\201RIO"},
    {"HOLD SELECTED HOUR", "FIXAR HORA"},
    {"WEATHER", "CLIMA"},
    {"CLEAR SKY", "C\303\211U LIMPO"},
    {"CLOUDY", "NUBLADO"},
    {"STORM", "TEMPESTADE"},
    {"FOG", "NEBLINA"},
    {"NATIVE ONLY", "AA NATIVO"},
    {"SHADOWS AND EFFECTS", "SOMBRAS E EFEITOS"},
    {"MP3 RADIO", "R\303\201DIO MP3"},
    {"PAUSE MUSIC", "PAUSAR"},
    {"MUSIC VOLUME", "VOLUME MP3"},
    {"PREVIOUS TRACK", "ANTERIOR"},
    {"NEXT TRACK", "PR\303\223XIMA"},
    {"SHOW TRACK", "MOSTRAR FAIXA"},
    {"RELOAD FOLDER", "RELER PASTA"},
    {"MUTE GAME MUSIC", "MUTAR TRILHA"},
    {"PAUSE ON ALT TAB", "PAUSAR ALT TAB"},
    {"PRESS", "EXECUTAR"},
    {"OFF (FASTER)", "N\303\203O (R\303\201PIDO)"},
    {"ON (CACHE MISS)", "SIM (CACHE)"},
    {"ON (SYNC)", "SIM (S\303\215NCRONO)"},
    {"FSR AND FRAMES", "FSR E QUADROS"},
    {"EFFECTS", "EFEITOS"},
    {"CAR VINYLS", "VINIS"},
  };
  auto exact = [&](const std::string& value) {
    for (const auto& pair : pairs) if (value == pair.en) return std::string(pair.pt);
    return value;
  };
  const auto translated = exact(text);
  if (translated != text) return translated;
  const auto bracket = text.find(" (");
  if (bracket != std::string::npos) {
    std::string result = exact(text.substr(0, bracket));
    size_t pos = bracket;
    while (pos < text.size()) {
      const auto end = text.find(')', pos);
      if (end == std::string::npos) return result + text.substr(pos);
      result += " (" + exact(text.substr(pos + 2, end - pos - 2)) + ")";
      pos = end + 1;
      if (pos < text.size() && text.substr(pos, 2) != " (") return result + text.substr(pos);
    }
    return result;
  }
  const auto rate = text.find(" PER SECOND");
  if (rate != std::string::npos) return text.substr(0, rate) + " POR SEGUNDO";
  return text;
}
