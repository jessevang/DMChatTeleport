# DM Chat Teleport & Starter Kits
A lightweight **server-side** mod for **7 Days to Die** (no client download required).
This mod adds teleport commands & starter kits.



---

## ✨ Features

### 🧭 Teleport Commands
Players can manage personal teleport locations:

| Command | Description |
|--------|-------------|
| `/setbase` | Saves the player's current location |
| `/base` | Teleports the player to their saved base |
| `/return` | Teleports the player back to their previous position |

Teleport commands can be enabled/disabled in the config.

---

### 🎁 Starter Kits
Players can select one pre-configured starter kit containing items you define:

- Configurable item names, quantities, and qualities  
- Supports random kit selection (`/pick Random`)  
- Bonus item for random picks  
- Fully server-side  

Commands:

```
/liststarterkits
/pick <kitname>
/pick Random
```

---

---

## 🛠 Installation

1. Extract the mod into your server’s `Mods` folder:

```
7DaysToDieServer/Mods/
```

2. Restart the server.

This mod is **server-side only** — players do *not* need to install anything.

---

## 🔧 Configuration

All options are stored in:

```
Mods/DMChatTeleport/Config.xml
```

Options include:

- Enable/Disable teleport commands  
- Enable/Disable starter kits  
- Define kit names, descriptions, items, quantities, quality  

Reload configuration at runtime:

```
/reloadconfig
```


## 📄 License
This project is licensed under the MIT License.  
See the `LICENSE` file for full details.
