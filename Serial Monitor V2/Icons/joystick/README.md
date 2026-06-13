# 摇杆大圆盘素材

> **功能**：把 PNG 放进来 → 摇杆面板下拉菜单自动出现你的风格。内置"手柄风/极简风/经典风"始终存在，不受影响。

---

## 命名规则

每个风格需要 **2 张图**：底座 pad（大底） + 拇指 thumb（可拖拽的圆钮）。

| 文件 | 尺寸 | 说明 |
|------|:---:|------|
| `pad_风格名.png` | **140×140** px | 摇杆底板（圆形/方形都行，软件自适应） |
| `thumb_风格名.png` | **32×32** px | 拇指/钮（会随拖拽移动） |

格式要求：**PNG**（推荐透明背景），文件名**小写英文 + 下划线**。

---

## 示例

假如你想加一个 "航天风" 风格：

```
Icons/joystick/
├── README.md              ← 本文件
├── pad_gamepad.png        ← 内置手柄风（可选覆盖）
├── thumb_gamepad.png
├── pad_minimal.png
├── thumb_minimal.png
├── pad_classic.png
├── thumb_classic.png
├── pad_aerospace.png      ← 你的：底座
└── thumb_aerospace.png    ← 你的：拇指
```

### 效果

打开软件 → 摇杆面板 → 点"手柄风 ▼"下拉菜单：

```
✓ 手柄风
  极简风
  经典风
────────────
  aerospace          ← 自动出现！
```

选 `aerospace` 后偏好自动保存，下次打开记忆。

---

## 不想用了

删掉 `pad_aerospace.png` + `thumb_aerospace.png` → 重启软件 → 菜单里消失。如果之前选了这个风格，下次开软件自动回退到"手柄风"。

---

## 内置三风格的图片

你**可以**放 `pad_gamepad.png` / `pad_minimal.png` / `pad_classic.png` 等 6 张图来**覆盖**内置风格的代码绘制。

- 放了 → AI 画的圆/圈/线被换成你的图
- 没放 → 照常用代码画
- 可以只覆盖其中一两个风格，互不影响
