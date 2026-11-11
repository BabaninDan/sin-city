"""
Генератор черновой карты прототипа "Город Греха" для TrenchBroom (Valve 220).

    python Tools/gen_prototype_map.py

Пишет Assets/Maps/MAP_Prototype.map. Дальше карту правят руками в TrenchBroom;
перезапуск скрипта ПЕРЕЗАПИШЕТ файл.

Масштаб Tremble: 64 юнита = 1 метр. Z — вверх.
"""

import os

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Maps", "MAP_Prototype.map")

T = 32          # толщина стен
H = 384         # высота потолка (6 м)
DOOR_H = 256    # высота проёма / двери (4 м)
GAP = 128       # половина ширины проёма (4 м проём)

FLOOR = ("game/Flor_Square", 0.5)
WALL = ("game/LowerWall", 0.15)
PAPER = ("game/PaperWall", 0.15)
DOOR = ("game/LowerWall", 0.5)
TRIGGER = ("game/Flor_Square", 0.5)

world = []
entities = []


def box(x1, y1, z1, x2, y2, z2, tex=WALL):
    x1, x2 = min(x1, x2), max(x1, x2)
    y1, y2 = min(y1, y2), max(y1, y2)
    z1, z2 = min(z1, z2), max(z1, z2)

    if x2 - x1 < 1 or y2 - y1 < 1 or z2 - z1 < 1:
        return None

    name, s = tex
    f = lambda v: str(int(v))
    sc = f"0 {s} {s}"

    planes = [
        f"( {f(x1)} {f(y1)} {f(z1)} ) ( {f(x1)} {f(y1 + 1)} {f(z1)} ) ( {f(x1)} {f(y1)} {f(z1 + 1)} ) {name} [ 0 -1 0 0 ] [ 0 0 -1 0 ] {sc}",
        f"( {f(x1)} {f(y1)} {f(z1)} ) ( {f(x1)} {f(y1)} {f(z1 + 1)} ) ( {f(x1 + 1)} {f(y1)} {f(z1)} ) {name} [ 1 0 0 0 ] [ 0 0 -1 0 ] {sc}",
        f"( {f(x1)} {f(y1)} {f(z1)} ) ( {f(x1 + 1)} {f(y1)} {f(z1)} ) ( {f(x1)} {f(y1 + 1)} {f(z1)} ) {name} [ -1 0 0 0 ] [ 0 -1 0 0 ] {sc}",
        f"( {f(x2)} {f(y2)} {f(z2)} ) ( {f(x2)} {f(y2 + 1)} {f(z2)} ) ( {f(x2 + 1)} {f(y2)} {f(z2)} ) {name} [ 1 0 0 0 ] [ 0 -1 0 0 ] {sc}",
        f"( {f(x2)} {f(y2)} {f(z2)} ) ( {f(x2 + 1)} {f(y2)} {f(z2)} ) ( {f(x2)} {f(y2)} {f(z2 + 1)} ) {name} [ -1 0 0 0 ] [ 0 0 -1 0 ] {sc}",
        f"( {f(x2)} {f(y2)} {f(z2)} ) ( {f(x2)} {f(y2)} {f(z2 + 1)} ) ( {f(x2)} {f(y2 + 1)} {f(z2)} ) {name} [ 0 1 0 0 ] [ 0 0 -1 0 ] {sc}",
    ]

    return "{\n" + "\n".join(planes) + "\n}"


def wb(*args, **kw):
    b = box(*args, **kw)
    if b:
        world.append(b)


def wall_x(y1, y2, xa, xb, gaps):
    """Стена вдоль X (север/юг): y1..y2 толщина, xa..xb длина, gaps — [(gx1, gx2)]."""
    cursor = xa
    for gx1, gx2 in sorted(gaps):
        wb(cursor, y1, 0, gx1, y2, H)
        wb(gx1, y1, DOOR_H, gx2, y2, H)          # перемычка над проёмом
        cursor = gx2
    wb(cursor, y1, 0, xb, y2, H)


def wall_y(x1, x2, ya, yb, gaps=()):
    """Стена вдоль Y (запад/восток)."""
    cursor = ya
    for gy1, gy2 in sorted(gaps):
        wb(x1, cursor, 0, x2, gy1, H)
        wb(x1, gy1, DOOR_H, x2, gy2, H)
        cursor = gy2
    wb(x1, cursor, 0, x2, yb, H)


def room(x1, y1, x2, y2, south=True, north=True, gap_s=True, gap_n=True):
    """Комната с внутренними границами x1..x2, y1..y2. Стены снаружи."""
    if south:
        wall_x(y1 - T, y1, x1 - T, x2 + T, [(-GAP, GAP)] if gap_s else [])
    if north:
        wall_x(y2, y2 + T, x1 - T, x2 + T, [(-GAP, GAP)] if gap_n else [])
    wall_y(x1 - T, x1, y1, y2)
    wall_y(x2, x2 + T, y1, y2)


def corridor(y1, y2, half=GAP):
    wall_y(-half - T, -half, y1, y2)
    wall_y(half, half + T, y1, y2)


def entity(classname, props=None, brushes=None):
    lines = ["{", f'"classname" "{classname}"']
    for k, v in (props or {}).items():
        lines.append(f'"{k}" "{v}"')
    for b in brushes or []:
        lines.append(b)
    lines.append("}")
    entities.append("\n".join(lines))


def point(classname, x, y, z, **props):
    p = {"origin": f"{int(x)} {int(y)} {int(z)}"}
    p.update({k: str(v) for k, v in props.items()})
    entity(classname, p)


def door(y_wall_south, arena, mode):
    """Дверь в проёме стены, которая начинается на y_wall_south (толщина T)."""
    b = box(-GAP, y_wall_south + 4, 0, GAP, y_wall_south + T - 4, DOOR_H, DOOR)
    entity("arena_door", {"arena": arena, "mode": mode}, [b])


def trigger(arena, x1, y1, x2, y2):
    b = box(x1, y1, 0, x2, y2, DOOR_H, TRIGGER)
    entity("arena_trigger", {"arena": arena}, [b])


def spawn(arena, kind, x, y, wave=1, count=1, suit="Random"):
    point("enemy_spawn", x, y, 16, arena=arena, enemy_type=kind, suit=suit, wave=wave, count=count)


def crate(x, y, ammo=30):
    point("prop_AmmoCrate", x, y, 0, chip_ammo=ammo)


def pillar(cx, cy, size=64, tex=PAPER):
    wb(cx - size, cy - size, 0, cx + size, cy + size, H, tex)


# =====================================================================
# LAYOUT (внутренние границы помещений)
# =====================================================================

# Комната героя
R0 = (-384, 0, 384, 768)
# Арены
A1 = (-768, 1312, 768, 2592)
A2 = (-896, 3168, 896, 4704)
A3 = (-1024, 5280, 1024, 7072)
# Выход
EX = (-256, 7104, 256, 7616)

# ---------------------------------------------------------------------
# Пол и потолок одним куском + внешняя "коробка", чтобы карта была герметичной
# ---------------------------------------------------------------------
MINX, MAXX = -1120, 1120
MINY, MAXY = -96, 7712

wb(MINX, MINY, -32, MAXX, MAXY, 0, FLOOR)
wb(MINX, MINY, H, MAXX, MAXY, H + 32, WALL)
wb(MINX - 32, MINY - 32, -32, MINX, MAXY + 32, H + 32)
wb(MAXX, MINY - 32, -32, MAXX + 32, MAXY + 32, H + 32)
wb(MINX, MINY - 32, -32, MAXX, MINY, H + 32)
wb(MINX, MAXY, -32, MAXX, MAXY + 32, H + 32)

# ---------------------------------------------------------------------
# Комната героя
# ---------------------------------------------------------------------
room(*R0, gap_s=False)
# Кровать / стол — просто блоки для уюта
wb(-352, 32, 0, -160, 224, 40, PAPER)
wb(-352, 32, 40, -320, 224, 96, PAPER)
wb(160, 32, 0, 352, 128, 72, WALL)

# Коридор к арене 1
corridor(R0[3] + T, A1[1] - T)

# ---------------------------------------------------------------------
# АРЕНА 1 — разминка: шулеры, потом крупье
# ---------------------------------------------------------------------
room(*A1)
pillar(-384, 1760)
pillar(384, 1760)
pillar(-384, 2208)
pillar(384, 2208)
wb(-160, 1936, 0, 160, 1984, 72, WALL)        # низкое укрытие

door(A1[1] - T, "1", "Entrance")
door(A1[3], "1", "Exit")
trigger("1", A1[0] + 64, A1[1] + 224, A1[2] - 64, A1[3] - 64)

spawn("1", "Shuler", -512, 2350)
spawn("1", "Shuler", 512, 2350)
spawn("1", "Shuler", 0, 2480, suit="Hearts")
spawn("1", "Shuler", -620, 1900, wave=2)
spawn("1", "Shuler", 620, 1900, wave=2, suit="Spades")
spawn("1", "Croupier", 0, 2480, wave=2)

crate(-660, 1420)
crate(660, 2480)

# Коридор 2 — пулемёт фишек
corridor(A1[3] + T, A2[1] - T)
point("prop_ChipGunPickup", 0, (A1[3] + A2[1]) // 2, 0, ammo=80)

# ---------------------------------------------------------------------
# АРЕНА 2 — платформы, крупье держат дистанцию, вышибала
# ---------------------------------------------------------------------
room(*A2)
wb(A2[0], 3520, 0, A2[0] + 384, 4096, 80, WALL)    # платформа слева
wb(A2[2] - 384, 3520, 0, A2[2], 4096, 80, WALL)    # платформа справа
wb(-224, 3712, 0, 224, 3760, 96, WALL)             # укрытия
wb(-224, 4240, 0, 224, 4288, 96, WALL)
pillar(-256, 4000, 48)
pillar(256, 4000, 48)

door(A2[1] - T, "2", "Entrance")
door(A2[3], "2", "Exit")
trigger("2", A2[0] + 64, A2[1] + 224, A2[2] - 64, A2[3] - 64)

spawn("2", "Croupier", -600, 4480)
spawn("2", "Croupier", 600, 4480)
spawn("2", "Shuler", -300, 4100)
spawn("2", "Shuler", 300, 4100)
spawn("2", "Bouncer", 0, 4560, wave=2)
spawn("2", "Shuler", -700, 3450, wave=2)
spawn("2", "Shuler", 700, 3450, wave=2)

crate(-820, 3260)
crate(820, 3260)
crate(0, 4620, ammo=40)

# Коридор 3
corridor(A2[3] + T, A3[1] - T)
crate(0, (A2[3] + A3[1]) // 2, ammo=40)

# ---------------------------------------------------------------------
# АРЕНА 3 — финальный зал (VIP): три волны
# ---------------------------------------------------------------------
room(*A3)
for cx, cy in [(-320, 5920), (320, 5920), (-320, 6432), (320, 6432)]:
    pillar(cx, cy, 64)
wb(-640, 6160, 0, -448, 6208, 80, WALL)
wb(448, 6160, 0, 640, 6208, 80, WALL)
wb(-96, 6624, 0, 96, 6672, 80, WALL)

door(A3[1] - T, "3", "Entrance")
door(A3[3], "3", "Exit")
trigger("3", A3[0] + 64, A3[1] + 224, A3[2] - 64, A3[3] - 64)

spawn("3", "Shuler", -620, 6500)
spawn("3", "Shuler", 0, 6880)
spawn("3", "Shuler", 620, 6500)
spawn("3", "Croupier", -860, 6940)
spawn("3", "Croupier", 860, 6940)

spawn("3", "Bouncer", -460, 6800, wave=2)
spawn("3", "Bouncer", 460, 6800, wave=2)
spawn("3", "Croupier", 0, 6960, wave=2, count=2)

spawn("3", "Bouncer", 0, 6750, wave=3)
spawn("3", "Shuler", -700, 6200, wave=3, count=2)
spawn("3", "Shuler", 700, 6200, wave=3, count=2)

crate(-920, 5380)
crate(920, 5380)
crate(-920, 6980, ammo=40)
crate(920, 6980, ammo=40)

# ---------------------------------------------------------------------
# Выход (конец главы)
# ---------------------------------------------------------------------
wall_y(EX[0] - T, EX[0], EX[1], EX[3])
wall_y(EX[2], EX[2] + T, EX[1], EX[3])
wall_x(EX[3], EX[3] + T, EX[0] - T, EX[2] + T, [])
entity("level_exit_trigger", {}, [box(-224, 7300, 0, 224, 7584, DOOR_H, TRIGGER)])

# ---------------------------------------------------------------------
# Игрок и автомат душ (в комнате)
# ---------------------------------------------------------------------
point("PlayerPrefab1", 0, 320, 72)
point("prop_UpgradeMachine", 256, 600, 0)
point("prop_AmmoCrate", -256, 600, 0, chip_ammo=20)

# =====================================================================
# WRITE
# =====================================================================

header = [
    "// Game: Unity: Sin City",
    "// Format: Quake3 (Valve)",
    "// Generated by Tools/gen_prototype_map.py",
]

out = list(header)
out.append("// entity 0")
out.append("{")
out.append('"mapversion" "220"')
out.append('"_tb_textures" "textures/game"')
out.append('"classname" "worldspawn"')
for i, b in enumerate(world):
    out.append(f"// brush {i}")
    out.append(b)
out.append("}")

for i, e in enumerate(entities, start=1):
    out.append(f"// entity {i}")
    out.append(e)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
    fh.write("\n".join(out) + "\n")

print(f"Wrote {os.path.abspath(OUT)}: {len(world)} world brushes, {len(entities)} entities")
