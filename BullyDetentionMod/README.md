# Bully Detention 67（校霸偷东西 → 校长拘留 67 秒）

一个《Baldi's Basics Plus》BepInEx 插件：**校霸（It's a Bully）成功偷走玩家一件物品时，校长会立刻出现并把玩家送进办公室拘留 67 秒。**

## 效果

- 校霸每次**成功偷到物品**都会触发一次拘留。
- 校长会把玩家传送到办公室，关闭并锁上办公室门，开始 67 秒拘留倒计时。
- 如果校霸只是撞了玩家、没有偷到东西（例如背包为空），不会触发。
- 如果当前楼层没有校长，插件会直接启动办公室拘留，仍然拘留 67 秒，避免效果失效。
- 默认每次都是 67 秒，不受校长原本“15/20/25/99 秒”递增规则影响。

## 安装

把压缩包里的 `BepInEx` 文件夹合并到游戏根目录即可，最终文件应为：

```
Baldi's Basics Plus\BepInEx\plugins\BullyDetention67\BullyDetention67.dll
```

本机已经安装好了。启动游戏后，`BepInEx\LogOutput.log` 中应出现：

```
[Info   :Bully Detention 67] Patch verified: Bully.StealItem -> Principal detention (67s default).
[Info   :Bully Detention 67] Bully Detention 67 v1.0.0 loaded.
```

## 配置

配置文件：`BepInEx\config\superhunt.bbpp.bullydetention67.cfg`

```ini
[Detention]
## 是否启用
Enabled = true

## 拘留时长，默认 67
Seconds = 67
```

修改后重启游戏生效。

## 关于“67 秒”

`Seconds` 对应游戏拘留界面上的倒计时数字（时钟和 HUD 量表显示 67）。游戏本身的拘留倒计时以约 1.25 倍速度推进，所以现实时间会比 67 秒略短，和游戏原版拘留的计时方式一致。

## 兼容性

- 游戏版本：Baldi's Basics Plus **0.14.5**
- 加载器：BepInEx **5.4.23.5**
- 已在本机完成编译、插件加载和 Harmony 补丁校验。
- 上一个“笔记本熄灯”插件仍保留在 `BepInEx\plugins\NotebookBlackout`。

## 卸载

删除 `BepInEx\plugins\BullyDetention67` 文件夹即可。

## 源码与重新编译

源码位于 `BullyDetentionMod\`：

```
dotnet build -c Release
```

生成：`BullyDetentionMod\bin\Release\netstandard2.0\BullyDetention67.dll`

如果游戏不在 `D:\game\steam\steamapps\common\Baldi's Basics Plus`，编译时加上：

```
dotnet build -c Release -p:GameDir="你的游戏目录"
```
