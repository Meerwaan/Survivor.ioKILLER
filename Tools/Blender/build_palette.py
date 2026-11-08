"""Rebuilds Assets/_Game/Art/Textures/T_Palette.png from cordon_kit.PALETTE (run after adding a swatch)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import cordon_kit as K

K.reset()
K.build_palette_image(save=True)
print("palette saved")
