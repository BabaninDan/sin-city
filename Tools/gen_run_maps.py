"""
Генерирует карты для роглайт-забега:

    python Tools/gen_run_maps.py

  Assets/Maps/MAP_Hub.map            — комната героя: игрок, автомат душ, портал «За стол»
  Assets/Maps/Rooms/ROOM_*.map       — шаблоны комнат забега

Шаблон комнаты = закрытый зал с энтити:
  room_info  (kinds: combat / boss / small, chapters: "123")
  room_entry (где появляется игрок)
  room_exit  (slot 0..2 — где встанут двери-выбор)
  enemy_spawn с role = Any / Melee / Ranged / Heavy

Это черновики: правь их в TrenchBroom или рисуй новые по тем же правилам.
Повторный запуск ПЕРЕЗАПИШЕТ файлы.
"""

import os
from mapgen import MapBuilder, WALL, PAPER

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Maps")
ROOMS = os.path.join(ROOT, "Rooms")


# ---------------------------------------------------------------------------
# Хаб — комната героя
# ---------------------------------------------------------------------------
def hub():
    m = MapBuilder()
    m.sealed_room(-512, 0, 512, 1024, PAPER)

    # Кровать, стол, шкаф
    m.box(-480, 32, 0, -256, 256, 40, PAPER)
    m.box(-480, 32, 40, -448, 256, 96, PAPER)
    m.box(256, 32, 0, 480, 160, 72, WALL)
    m.box(-480, 600, 0, -416, 900, 160, WALL)

    m.point("PlayerPrefab1", 0, 256, 72)
    m.point("prop_UpgradeMachine", 320, 800, 0)
    m.point("run_start", 0, 940, 0)
    m.crate(-300, 820, 10)

    m.write(os.path.join(ROOT, "MAP_Hub.map"))


# ---------------------------------------------------------------------------
# Шаблоны комнат
# ---------------------------------------------------------------------------
def hall_a():
    """Квадратный зал 20×20 м, четыре колонны, низкое укрытие в центре."""
    m = MapBuilder()
    m.sealed_room(-640, 0, 640, 1280)

    for cx, cy in [(-320, 448), (320, 448), (-320, 896), (320, 896)]:
        m.pillar(cx, cy)
    m.cover(-160, 640, 160, 688)

    m.room_info(0, 40, "combat")
    m.room_entry(0, 128)
    for i, x in enumerate([-384, 0, 384]):
        m.room_exit(x, 1184, i)

    for x, y in [(-480, 1000), (480, 1000), (0, 1060), (-520, 640), (520, 640), (0, 860)]:
        m.spawn(x, y, "Any")
    m.spawn(-576, 1150, "Ranged")
    m.spawn(576, 1150, "Ranged")
    m.spawn(0, 960, "Heavy")

    m.crate(-560, 120)
    m.crate(560, 120)

    m.write(os.path.join(ROOMS, "ROOM_Hall_A.map"))


def hall_b():
    """Широкий зал 28×16 м с двумя платформами по бокам."""
    m = MapBuilder()
    m.sealed_room(-896, 0, 896, 1024)

    m.cover(-896, 384, -576, 704, 80)
    m.cover(576, 384, 896, 704, 80)
    m.cover(-224, 448, -160, 576, 96)
    m.cover(160, 448, 224, 576, 96)
    m.pillar(0, 512, 48)

    m.room_info(0, 40, "combat")
    m.room_entry(0, 96)
    for i, x in enumerate([-448, 0, 448]):
        m.room_exit(x, 940, i)

    for x, y in [(-400, 820), (400, 820), (0, 860), (-640, 240), (640, 240), (-300, 380), (300, 380)]:
        m.spawn(x, y, "Any")
    m.spawn(-736, 544, "Ranged")
    m.spawn(736, 544, "Ranged")
    m.spawn(0, 760, "Heavy")

    m.crate(-820, 80)
    m.crate(820, 80)

    m.write(os.path.join(ROOMS, "ROOM_Hall_B.map"))


def long_c():
    """Длинный коридор-зал 14×28 м, укрытия зигзагом."""
    m = MapBuilder()
    m.sealed_room(-448, 0, 448, 1792)

    m.cover(-448, 448, -96, 496, 96)
    m.cover(96, 896, 448, 944, 96)
    m.cover(-448, 1344, -96, 1392, 96)

    m.room_info(0, 40, "combat", "123")
    m.room_entry(0, 96)
    for i, x in enumerate([-256, 0, 256]):
        m.room_exit(x, 1700, i)

    for x, y in [(-300, 700), (300, 600), (0, 1100), (-300, 1200), (300, 1500), (0, 1600)]:
        m.spawn(x, y, "Any")
    m.spawn(-360, 1000, "Ranged")
    m.spawn(360, 1300, "Ranged")
    m.spawn(0, 1450, "Heavy")

    m.crate(-380, 120)
    m.crate(380, 1250)

    m.write(os.path.join(ROOMS, "ROOM_Long_C.map"))


def lounge():
    """Небольшой лаунж для магазина / бара / рулетки (без врагов)."""
    m = MapBuilder(height=320)
    m.sealed_room(-448, 0, 448, 768, PAPER)

    m.cover(-320, 448, 320, 512, 72, WALL)     # стойка
    m.box(-448, 640, 0, -384, 768, 200, WALL)  # шкафы
    m.box(384, 640, 0, 448, 768, 200, WALL)

    m.room_info(0, 40, "small")
    m.room_entry(0, 96)
    for i, x in enumerate([-256, 0, 256]):
        m.room_exit(x, 690, i)

    m.write(os.path.join(ROOMS, "ROOM_Lounge.map"))


def boss_arena():
    """Арена босса 32×32 м: кольцо колонн вокруг центра, один выход."""
    m = MapBuilder(height=448)
    m.sealed_room(-1024, 0, 1024, 2048)

    import math
    for i in range(6):
        a = math.radians(i * 60 + 30)
        m.pillar(int(math.cos(a) * 480), int(1024 + math.sin(a) * 480), 64)

    m.room_info(0, 40, "boss")
    m.room_entry(0, 128)
    m.room_exit(0, 1950, 0)

    for x, y in [(0, 1400), (-640, 1600), (640, 1600)]:
        m.spawn(x, y, "Heavy")
    for x, y in [(-900, 1900), (900, 1900), (-900, 1000), (900, 1000)]:
        m.spawn(x, y, "Ranged")
    for x, y in [(-300, 1700), (300, 1700), (0, 1800), (-700, 700), (700, 700), (0, 1024)]:
        m.spawn(x, y, "Any")

    m.crate(-940, 100, 40)
    m.crate(940, 100, 40)

    m.write(os.path.join(ROOMS, "ROOM_Boss.map"))


if __name__ == "__main__":
    hub()
    hall_a()
    hall_b()
    long_c()
    lounge()
    boss_arena()
