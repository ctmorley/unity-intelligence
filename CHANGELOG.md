# Changelog

All notable changes to Unity Intelligence will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2025-01-04

### Added

#### Core Features
- **Multi-Provider AI Support**: Seamless integration with Anthropic (Claude), OpenAI (GPT-4), and Google (Gemini)
- **150+ AI-Callable Tools**: Complete control over Unity Editor operations
- **SSE Streaming**: Real-time streaming responses in the Unity Editor
- **Agentic Behavior**: AI executes tasks autonomously instead of just explaining

#### Tool Categories
- **Script & Shader Tools**: Create, modify, read, edit scripts with granular control
- **Scene Management**: Full scene lifecycle and hierarchy control
- **Asset Management**: Search, create, move, copy, delete assets
- **Prefab Tools**: Create, instantiate, modify prefabs
- **Material & Shader Tools**: Material creation and property editing
- **Transform & Hierarchy**: Position, rotation, scale, parenting
- **Component Tools**: Add, remove, configure any component
- **Physics Tools**: Rigidbodies, colliders, raycasts, joints
- **UI Tools**: Canvas, buttons, text, images, layout groups
- **Animation Tools**: Animator controllers, states, transitions, clips
- **Console & Diagnostics**: Read console logs, auto-detect and fix errors
- **Editor Utilities**: Selection, undo/redo, play mode, project settings

#### UI
- Dockable AI Assistant window (Window → AI Assistant)
- Model selector dropdown
- Chat interface with markdown support
- Code blocks with syntax highlighting and copy button
- Settings panel in Project Settings

#### Security
- API keys stored locally with EditorPrefs obfuscation
- No external data transmission except to AI providers

### Technical
- Attribute-based tool discovery (`[AITool]` and `[AIToolParameter]`)
- Main-thread execution guarantee for all Unity API calls
- Reflection-based console log access for diagnostics
- Unity 2021.3+ compatibility

---

## Future Roadmap

### [0.2.0] - Planned
- [ ] OpenAI provider implementation
- [ ] Google Gemini provider implementation
- [ ] Vision support (send screenshots to AI)
- [ ] Conversation persistence
- [ ] Token usage tracking and display

### [0.3.0] - Planned
- [ ] RAG support (codebase indexing)
- [ ] Multi-file context awareness
- [ ] Custom tool creation wizard
- [ ] Keyboard shortcuts customization
