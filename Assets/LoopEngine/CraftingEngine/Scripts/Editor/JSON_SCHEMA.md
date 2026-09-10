# Esquema JSON de contenido — LoopEngine.Crafting

Formato de autoría para crear y actualizar assets desde un archivo de texto. Solo existe en el editor: nada de esto se compila en una build.

Versión soportada: **1**

---

## Estructura raíz

```json
{
  "version": 1,
  "items": [],
  "categories": [],
  "recipes": [],
  "machines": [],
  "modifiers": []
}
```

Las cinco secciones son opcionales. Un archivo que solo trae `recipes` es válido, siempre que los items a los que apunta ya existan como assets.

---

## `items`

| Campo | Tipo | Por defecto | Notas |
|---|---|---|---|
| `id` | string | — | Obligatorio, permanente |
| `displayName` | string | `""` | Si está vacío se muestra el id |
| `description` | string | `""` | |
| `maxStack` | int | `0` | Declarativo. 0 = sin límite declarado |
| `tags` | string[] | `[]` | Etiquetas libres: `food`, `metal`, `tier2` |

```json
{ "id": "item.iron_ore", "displayName": "Iron Ore", "maxStack": 100, "tags": ["ore", "metal"] }
```

## `categories`

| Campo | Tipo | Por defecto |
|---|---|---|
| `id` | string | — |
| `displayName` | string | `""` |
| `description` | string | `""` |

## `recipes`

| Campo | Tipo | Por defecto | Notas |
|---|---|---|---|
| `id` | string | — | |
| `displayName` | string | `""` | |
| `inputs` | amount[] | `[]` | Puede estar vacío (generadores) |
| `outputs` | amount[] | `[]` | **Al menos uno**, o se rechaza |
| `craftSeconds` | float | `1.0` | 0 completa en el mismo tick |
| `requiredCategory` | string | `""` | Vacío = crafteo a mano |

Un `amount` es `{ "item": "<id>", "amount": <int ≥ 1> }`.

```json
{
  "id": "recipe.iron_ingot",
  "inputs":  [ { "item": "item.iron_ore", "amount": 2 },
               { "item": "item.coal", "amount": 1 } ],
  "outputs": [ { "item": "item.iron_ingot", "amount": 1 } ],
  "craftSeconds": 2.0,
  "requiredCategory": "category.furnace"
}
```

## `machines`

| Campo | Tipo | Por defecto | Notas |
|---|---|---|---|
| `id` | string | — | |
| `displayName` | string | `""` | |
| `categories` | string[] | `[]` | Capacidades que provee |
| `parallelSlots` | int | `1` | Mínimo 1 |
| `queueCapacity` | int | `4` | 0 desactiva la cola |
| `speedMultiplier` | float | `1.0` | Debe ser > 0 |
| `modifierSlots` | int | `0` | 0 desactiva las mejoras |
| `allowHandCraftedRecipes` | bool | `false` | |

Sin `categories` y sin `allowHandCraftedRecipes` la máquina no puede ejecutar nada: se rechaza.

## `modifiers`

| Campo | Tipo | Por defecto | Notas |
|---|---|---|---|
| `id` | string | — | |
| `displayName` | string | `""` | |
| `carrierItem` | string | `""` | Item que el jugador instala |
| `entries` | statEntry[] | `[]` | |
| `bonusOutputs` | amount[] | `[]` | Extra por craft completado |
| `allowedCategories` | string[] | `[]` | Vacío = cualquier máquina |
| `maxPerMachine` | int | `1` | Mínimo 1 |

Un `statEntry` es:

```json
{ "stat": "speed", "operation": "additive", "value": 0.25 }
```

- `stat`: `"speed"` o `"yield"` (sin distinguir mayúsculas)
- `operation`: `"additive"` o `"multiplicative"`
- `value`: en aditivo es la fracción sumada (`0.25` = +25%); en multiplicativo es el factor (`1.5` = ×1.5)

Sin `entries` y sin `bonusOutputs`, el modificador no hace nada y se rechaza.

---

## Limitaciones del formato

Vienen de `JsonUtility`, que es lo que usa el sistema para no añadir dependencias externas:

- **No hay números de línea en los errores de sintaxis.** `JsonUtility` lanza una excepción sin posición. Los errores estructurales (los que sí puede detectar el parser) sí indican sección e índice: `recipes[3] 'recipe.x' has no outputs`.
- **Los campos desconocidos se ignoran en silencio.** Un `craftSecond` mal escrito no da error; el campo simplemente se queda en su valor por defecto.
- **No se distingue ausente de cero.** Un campo que falta toma el valor por defecto de la tabla. Por eso `maxStack: 0` significa "sin límite" y no "no se puede apilar".
- **No hay comentarios en JSON.** Si necesitas notas, usa `description`.
- **Los enums van como texto**, no como número, porque `JsonUtility` no convierte strings a enums por su cuenta. La conversión la hace el parser.

---

## Menús

```
Tools/LoopEngine/Crafting/JSON/Write Example File...
Tools/LoopEngine/Crafting/JSON/Validate File...
```

`Validate File` parsea y comprueba el archivo **sin tocar el AssetDatabase**. Es la forma segura de ver qué se quejaría una importación antes de ejecutarla.

---

## Fuentes

- [JsonUtility.FromJson — Scripting API 6000.0](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.FromJson.html)
- [JsonUtility.ToJson — Scripting API 6000.0](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/JsonUtility.ToJson.html)
- [Serialización de scripts — Manual 6000.0](https://docs.unity3d.com/6000.0/Documentation/Manual/script-serialization.html)
