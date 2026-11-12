# 发布 DeltaTrack

日常提交只运行 CI。正式发布 GitHub Release 时，`publish.yml` 才会把测试通过的包上传到 nuget.org。

## 首次配置

1. 工作流中的 NuGet 用户名已配置为 `XBlueC`（个人资料名称，不是邮箱），不需要另外配置 GitHub 变量或密钥。更换发布账号时，需要同时修改
   `publish.yml` 的 `NuGet login` 步骤和对应的 NuGet 信任策略。
2. 登录 nuget.org，在头像菜单的 **Trusted Publishing** 中创建 GitHub 策略：

   | 配置项           | 值                             |
   |------------------|--------------------------------|
   | Repository Owner | `XBlueC`                       |
   | Repository       | `DeltaTrack`                   |
   | Workflow File    | `publish.yml`（仅文件名）      |
   | Environment      | 留空，工作流未指定 environment |

   选择拥有 `DeltaTrack` 包的账号或组织；如果页面提供包名称匹配范围，将其限定为 `DeltaTrack`。

工作流通过 `NuGet/login` 使用 OIDC 获取临时发布凭据，不需要保存长期 API Key。

参考：[NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)。

## 每次发布

1. 修改 `DeltaTrack/DeltaTrack.csproj` 的 `<Version>` 并提交，确认 `main` 的 CI 通过。
2. 给准备发布的提交创建带说明的标签，再推送标签。例如：

   ```powershell
   git push origin main
   git tag -a v1.1.2 -m "DeltaTrack 1.1.2"
   git push origin v1.1.2
   ```

3. 如需先验证，在 GitHub **Actions → Publish NuGet → Run workflow** 中填写已有标签。这条手动路径只测试、打包和保存构建产物，不上传
   NuGet。
4. 在仓库 **Releases → Draft a new release** 中选择该标签，填写更新说明，再点击 **Publish release**。
5. 检查 **Actions → Publish NuGet**：工作流校验版本、运行 Release 测试、打包并验证运行库和源生成器文件，再上传同一份包到
   nuget.org。

标签去掉 `v` 后，必须与项目版本完全一致。工作流不会覆盖项目版本，也不会因为普通分支推送或仅推送标签而发布 NuGet。

预发布示例：项目版本 `1.2.0-rc.1`，标签 `v1.2.0-rc.1`，GitHub Release 勾选 **This is a pre-release**。稳定版不勾选；工作流会检查两者一致。

发布失败时先检查 Actions 日志。首次配置常见问题是 NuGet 用户名写成邮箱、信任策略中的工作流文件名不匹配，或标签与项目版本不一致。解决后可以重新运行失败任务。

已经成功上传的包版本不能用新内容覆盖；后续修复应提升版本，并创建新的标签和 Release。

## 版本选择

| 变化                        | 版本示例        |
|-----------------------------|-----------------|
| 兼容的缺陷修复              | `1.1.1 → 1.1.2` |
| 兼容的新功能                | `1.1.2 → 1.2.0` |
| 不兼容的公开 API 或行为调整 | `1.x → 2.0.0`   |
| 候选版本                    | `1.2.0-rc.1`    |

Tag 用于定位发布源码，Release 用于更新说明，NuGet 版本用于安装包。保留已发布标签指向的提交，确保用户可以找到对应源码。
