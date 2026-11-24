#pragma once
#include <memory>
#include <string>
namespace rex::ui {class ImGuiDialog;class ImGuiDrawer;}
// 1 previous, 2 next, 3 rescan, 4 show track.
void MclaRadioCommand(int command);
void MclaRadioInstallHotkeys();
void MclaRadioTick();
std::string MclaRadioStatus();
std::unique_ptr<rex::ui::ImGuiDialog> MclaRadioCreateOverlay(rex::ui::ImGuiDrawer* drawer);
