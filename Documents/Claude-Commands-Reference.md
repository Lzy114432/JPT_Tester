# Claude Code 命令参考手册

## 快速导航

- [开发流程命令](#开发流程命令)
- [代码质量命令](#代码质量命令)
- [架构与设计命令](#架构与设计命令)
- [任务管理命令](#任务管理命令)
- [基础设施命令](#基础设施命令)
- [开发语言专家命令](#开发语言专家命令)
- [数据与AI命令](#数据与ai命令)
- [Skills 技能命令](#skills-技能命令)
- [使用示例](#使用示例)

---

## 开发流程命令

### 修改前

| 命令 | 用途 | 使用场景 |
|------|------|----------|
| `/agents:task-decomposer` | 任务分解 | 把大任务拆成小步骤 |
| `/agents:architecture-auditor` | 架构审查 | 新功能设计、重构规划 |
| `/agents:project-architect` | 项目架构师 | 新项目初始化、重大功能规划 |

### 修改中

| 命令 | 用途 | 使用场景 |
|------|------|----------|
| `/agents:legacy-modernizer` | 遗留代码现代化 | 重构老代码、升级框架 |
| `/agents:task-orchestrator` | 任务编排 | 协调多步骤并行任务 |
| `/agents:dependency-analyzer` | 依赖分析 | 检查依赖冲突、更新依赖 |

### 修改后

| 命令 | 用途 | 使用场景 |
|------|------|----------|
| `/agents:code-auditor` | 代码审查 | 检查代码质量、安全性 |
| `/agents:test-engineer` | 测试工程师 | 生成单元测试、集成测试 |
| `/agents:performance-auditor` | 性能审查 | 优化性能、找瓶颈 |
| `/agents:task-commit-manager` | 提交管理 | 规范化 git 提交 |

---

## 代码质量命令

| 命令 | 描述 |
|------|------|
| `/agents:code-auditor` | 代码质量检查，识别代码异味、安全问题、性能问题 |
| `/agents:test-engineer` | 自动生成测试用例，确保测试覆盖率 |
| `/agents:performance-auditor` | 性能优化专家，分析速度、效率、资源使用 |

---

## 架构与设计命令

| 命令 | 描述 |
|------|------|
| `/agents:architecture-auditor` | 软件架构和设计模式专家 |
| `/agents:project-architect` | 项目初始化和最佳实践设置 |
| `/agents:strategic-analyst` | 业务和技术场景建模，战略规划 |
| `/agents:legacy-modernizer` | 遗留系统现代化，安全重构 |

---

## 任务管理命令

| 命令 | 描述 |
|------|------|
| `/agents:task-decomposer` | 将复杂项目分解为原子化、可执行的任务 |
| `/agents:task-orchestrator` | 编排多步骤任务，管理并行执行 |
| `/agents:task-commit-manager` | 管理任务完成和 git 提交工作流 |
| `/agents:TASK-STATUS-PROTOCOL` | 定义和管理任务状态转换 |
| `/agents:agent-organizer` | 多代理任务编排器，分析项目需求并组织代理团队 |

---

## 基础设施命令

| 命令 | 描述 |
|------|------|
| `/agents:infrastructure:cloud-architect` | AWS/Azure/GCP 云架构设计，Terraform IaC |
| `/agents:infrastructure:deployment-engineer` | CI/CD 流水线，容器编排，云基础设施自动化 |
| `/agents:infrastructure:incident-responder` | 生产事故响应，SRE 最佳实践 |
| `/agents:infrastructure:devops-incident-responder` | 事故响应和根因分析 |
| `/agents:infrastructure:performance-engineer` | 性能策略，跨团队优化 |
| `/agents:azure-devops-specialist` | Azure DevOps 和云基础设施专家 |

---

## 开发语言专家命令

### 前端

| 命令 | 描述 |
|------|------|
| `/agents:development:frontend-developer` | React 组件开发，前端架构 |
| `/agents:development:react-pro` | React 专家，Hooks、Context API、状态管理 |
| `/agents:development:nextjs-pro` | Next.js 专家，SSR/SSG、App Router |
| `/agents:development:ui-designer` | UI 界面设计，设计系统 |
| `/agents:development:ux-designer` | 用户体验设计，可用性优化 |

### 后端

| 命令 | 描述 |
|------|------|
| `/agents:development:backend-architect` | 后端系统设计，可扩展架构 |
| `/agents:development:full-stack-developer` | 全栈开发，前后端集成 |
| `/agents:development:python-pro` | Python 专家，装饰器、生成器、async |
| `/agents:development:golang-pro` | Go 专家，并发、高性能应用 |
| `/agents:development:typescript-pro` | TypeScript 专家，类型安全、可维护性 |

### 桌面/移动

| 命令 | 描述 |
|------|------|
| `/agents:development:electron-pro` | Electron 跨平台桌面应用开发 |
| `/agents:development:mobile-developer` | React Native/Flutter 移动开发 |
| `/agents:swift-macos-expert` | Swift/macOS 桌面应用开发 |

### Svelte 专项

| 命令 | 描述 |
|------|------|
| `/agents:svelte-development` | Svelte 5+ 和 SvelteKit 开发 |
| `/agents:svelte-testing` | Svelte 测试，Vitest/Playwright |
| `/agents:svelte-storybook` | Svelte Storybook 组件文档 |

### 其他

| 命令 | 描述 |
|------|------|
| `/agents:development:dx-optimizer` | 开发者体验优化，工具链改进 |

---

## 数据与AI命令

| 命令 | 描述 |
|------|------|
| `/agents:data-ai:prompt-engineer` | 提示词工程，LLM 交互优化 |
| `/agents:data-ai:ml-engineer` | 机器学习模型生命周期管理 |
| `/agents:data-ai:graphql-architect` | GraphQL API 设计和优化 |
| `/agents:data-ai:postgresql-pglite-pro` | PostgreSQL/Pglite 数据库专家 |

---

## 业务命令

| 命令 | 描述 |
|------|------|
| `/agents:business:product-manager` | 产品策略、路线图、跨团队协调 |
| `/agents:integration-manager` | GitHub/Linear 跨平台同步 |
| `/agents:release-manager` | 发布准备、版本管理、部署 |

---

## Skills 技能命令

| 命令 | 描述 |
|------|------|
| `/skills:linear-todo-sync` | 从 Linear 同步任务生成本地 TODO 列表 |
| `/skills:cloudflare-manager` | Cloudflare Workers/KV/R2/Pages 管理 |

---

## 使用示例

### 场景 1：简单 Bug 修复

```
你：帮我修复 XXX 的空指针问题
Claude：（直接修复）

你：/agents:code-auditor
Claude：（检查修复质量）
```

### 场景 2：新功能开发

```
你：/agents:task-decomposer 我要添加一个自动复位功能

Claude：（输出任务步骤）

你：开始执行

Claude：（逐步实现）

你：/agents:test-engineer
Claude：（生成测试）

你：/agents:code-auditor
Claude：（最终检查）
```

### 场景 3：重构遗留代码

```
你：/agents:architecture-auditor 分析 ResetLogic 类的问题

Claude：（输出架构分析）

你：/agents:legacy-modernizer 按建议重构

Claude：（执行重构）

你：/agents:code-auditor
Claude：（检查重构结果）
```

### 场景 4：性能优化

```
你：/agents:performance-auditor 分析主流程的性能瓶颈

Claude：（分析并给出优化建议）

你：执行优化方案 1

Claude：（执行优化）
```

---

## 命令使用技巧

1. **可以带上下文**
   ```
   /agents:code-auditor 重点检查 XXX 文件
   ```

2. **可以连续使用**
   ```
   /agents:task-decomposer → 执行 → /agents:code-auditor
   ```

3. **简单任务不需要命令**
   ```
   直接说：帮我把超时时间改成 10 秒
   ```

---

## 本项目常用组合

| 工作类型 | 推荐命令组合 |
|----------|--------------|
| Bug 修复 | 直接说 → `code-auditor` |
| 新功能 | `task-decomposer` → 执行 → `test-engineer` → `code-auditor` |
| 重构 | `architecture-auditor` → `legacy-modernizer` → `code-auditor` |
| 性能优化 | `performance-auditor` → 执行 → `code-auditor` |

---

*文档生成时间: 2025-12-28*
