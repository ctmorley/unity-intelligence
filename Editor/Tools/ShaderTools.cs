using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityAIAssistant.Core.Tools;

namespace UnityAIAssistant.Editor.Tools
{
    /// <summary>
    /// AI tools for shader creation and management.
    /// </summary>
    public static class ShaderTools
    {
        #region Shader Creation

        [AITool("create_shader", "Create a new shader file")]
        public static AIToolResult CreateShader(
            [AIToolParameter("Path relative to Assets folder (e.g., 'Shaders/MyShader.shader')")] string path,
            [AIToolParameter("Complete shader source code (ShaderLab format)")] string content)
        {
            try
            {
                if (!path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".shader";
                }

                string fullPath = Path.Combine(Application.dataPath, path);
                string directory = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Shader already exists at Assets/{path}. Use modify_shader to update it.");
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                // Check for compile errors
                var shader = AssetDatabase.LoadAssetAtPath<Shader>($"Assets/{path}");
                if (shader != null && ShaderUtil.ShaderHasError(shader))
                {
                    return AIToolResult.Succeeded($"Created shader at Assets/{path} (Warning: shader has compile errors)");
                }

                return AIToolResult.Succeeded($"Created shader at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create shader: {ex.Message}");
            }
        }

        [AITool("modify_shader", "Modify an existing shader file", requiresConfirmation: true)]
        public static AIToolResult ModifyShader(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("New complete content for the shader")] string content)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Shader not found at Assets/{path}");
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                var shader = AssetDatabase.LoadAssetAtPath<Shader>($"Assets/{path}");
                if (shader != null && ShaderUtil.ShaderHasError(shader))
                {
                    return AIToolResult.Succeeded($"Modified shader at Assets/{path} (Warning: shader has compile errors)");
                }

                return AIToolResult.Succeeded($"Modified shader at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to modify shader: {ex.Message}");
            }
        }

        [AITool("read_shader", "Read the contents of a shader file")]
        public static AIToolResult ReadShader(
            [AIToolParameter("Path relative to Assets folder")] string path)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, path);

                if (!File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Shader not found at Assets/{path}");
                }

                string content = File.ReadAllText(fullPath);
                return AIToolResult.Succeeded(content);
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to read shader: {ex.Message}");
            }
        }

        [AITool("create_shader_from_template", "Create a shader from a built-in template")]
        public static AIToolResult CreateShaderFromTemplate(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Shader name (displayed in Unity)")] string shaderName,
            [AIToolParameter("Template: 'unlit', 'surface', 'urp_lit', 'urp_unlit', 'image_effect', 'vertex_fragment'")] string template)
        {
            try
            {
                if (!path.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".shader";
                }

                string fullPath = Path.Combine(Application.dataPath, path);

                if (File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Shader already exists at Assets/{path}");
                }

                string content = GetShaderTemplate(template, shaderName);
                if (string.IsNullOrEmpty(content))
                {
                    return AIToolResult.Failed($"Unknown template: {template}");
                }

                string directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Created {template} shader at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create shader: {ex.Message}");
            }
        }

        #endregion

        #region Shader Include Files

        [AITool("create_cginc", "Create a shader include file (.cginc or .hlsl)")]
        public static AIToolResult CreateCGInclude(
            [AIToolParameter("Path relative to Assets folder (e.g., 'Shaders/Includes/Utils.cginc')")] string path,
            [AIToolParameter("Include file content (HLSL/CG code)")] string content)
        {
            try
            {
                if (!path.EndsWith(".cginc", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".hlsl", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".cginc";
                }

                string fullPath = Path.Combine(Application.dataPath, path);
                string directory = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Created include file at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create include file: {ex.Message}");
            }
        }

        #endregion

        #region Shader Info

        [AITool("get_shader_info", "Get information about a shader")]
        public static AIToolResult GetShaderInfo(
            [AIToolParameter("Shader name or asset path")] string shaderNameOrPath)
        {
            try
            {
                Shader shader;

                if (shaderNameOrPath.StartsWith("Assets/"))
                {
                    shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderNameOrPath);
                }
                else
                {
                    shader = Shader.Find(shaderNameOrPath);
                }

                if (shader == null)
                {
                    return AIToolResult.Failed($"Shader not found: {shaderNameOrPath}");
                }

                var path = AssetDatabase.GetAssetPath(shader);
                bool hasErrors = ShaderUtil.ShaderHasError(shader);
                int propertyCount = ShaderUtil.GetPropertyCount(shader);

                var properties = new object[propertyCount];
                for (int i = 0; i < propertyCount; i++)
                {
                    properties[i] = new
                    {
                        name = ShaderUtil.GetPropertyName(shader, i),
                        description = ShaderUtil.GetPropertyDescription(shader, i),
                        type = ShaderUtil.GetPropertyType(shader, i).ToString()
                    };
                }

                var info = new
                {
                    name = shader.name,
                    path,
                    isSupported = shader.isSupported,
                    hasErrors,
                    renderQueue = shader.renderQueue,
                    passCount = shader.passCount,
                    propertyCount,
                    properties
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get shader info: {ex.Message}");
            }
        }

        [AITool("get_shader_keywords", "Get keywords defined in a shader")]
        public static AIToolResult GetShaderKeywords(
            [AIToolParameter("Shader name or asset path")] string shaderNameOrPath)
        {
            try
            {
                Shader shader = shaderNameOrPath.StartsWith("Assets/")
                    ? AssetDatabase.LoadAssetAtPath<Shader>(shaderNameOrPath)
                    : Shader.Find(shaderNameOrPath);

                if (shader == null)
                {
                    return AIToolResult.Failed($"Shader not found: {shaderNameOrPath}");
                }

                // Read the shader source to find keywords
                var path = AssetDatabase.GetAssetPath(shader);
                if (string.IsNullOrEmpty(path))
                {
                    return AIToolResult.Failed("Shader is a built-in shader, cannot read source");
                }

                string content = File.ReadAllText(path);

                // Find pragma multi_compile and shader_feature directives
                var multiCompile = Regex.Matches(content, @"#pragma\s+multi_compile[_local]*\s+(.+)$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToArray();

                var shaderFeature = Regex.Matches(content, @"#pragma\s+shader_feature[_local]*\s+(.+)$", RegexOptions.Multiline)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .ToArray();

                var info = new
                {
                    shaderName = shader.name,
                    multiCompileKeywords = multiCompile,
                    shaderFeatureKeywords = shaderFeature
                };

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(info, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to get shader keywords: {ex.Message}");
            }
        }

        [AITool("list_shaders_in_project", "List all shader files in the project")]
        public static AIToolResult ListShadersInProject(
            [AIToolParameter("Folder to search (e.g., 'Assets/Shaders')", isOptional: true)] string folder = "Assets")
        {
            try
            {
                var guids = AssetDatabase.FindAssets("t:Shader", new[] { folder });
                var shaders = guids.Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                    return new
                    {
                        name = shader?.name ?? "Unknown",
                        path,
                        isSupported = shader?.isSupported ?? false,
                        hasErrors = shader != null && ShaderUtil.ShaderHasError(shader)
                    };
                }).ToArray();

                return AIToolResult.Succeeded($"Found {shaders.Length} shaders", new { shaders });
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to list shaders: {ex.Message}");
            }
        }

        [AITool("check_shader_errors", "Check a shader for compilation errors")]
        public static AIToolResult CheckShaderErrors(
            [AIToolParameter("Shader asset path")] string path)
        {
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    return AIToolResult.Failed($"Shader not found: {path}");
                }

                bool hasErrors = ShaderUtil.ShaderHasError(shader);

                if (hasErrors)
                {
                    // Get error count from ShaderUtil (internal API varies by Unity version)
                    return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                    {
                        shaderName = shader.name,
                        hasErrors = true,
                        message = "Shader has compilation errors. Check the Console window for details."
                    }, Formatting.Indented));
                }

                return AIToolResult.Succeeded(JsonConvert.SerializeObject(new
                {
                    shaderName = shader.name,
                    hasErrors = false,
                    message = "Shader compiled successfully"
                }, Formatting.Indented));
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to check shader: {ex.Message}");
            }
        }

        #endregion

        #region Compute Shaders

        [AITool("create_compute_shader", "Create a new compute shader")]
        public static AIToolResult CreateComputeShader(
            [AIToolParameter("Path relative to Assets folder (e.g., 'Shaders/MyCompute.compute')")] string path,
            [AIToolParameter("Complete compute shader source code")] string content)
        {
            try
            {
                if (!path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".compute";
                }

                string fullPath = Path.Combine(Application.dataPath, path);
                string directory = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(fullPath))
                {
                    return AIToolResult.Failed($"Compute shader already exists at Assets/{path}");
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Created compute shader at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create compute shader: {ex.Message}");
            }
        }

        [AITool("create_compute_shader_template", "Create a compute shader from template")]
        public static AIToolResult CreateComputeShaderTemplate(
            [AIToolParameter("Path relative to Assets folder")] string path,
            [AIToolParameter("Kernel name")] string kernelName,
            [AIToolParameter("Template: 'basic', 'texture', 'buffer'")] string template = "basic")
        {
            try
            {
                if (!path.EndsWith(".compute", StringComparison.OrdinalIgnoreCase))
                {
                    path += ".compute";
                }

                string content = GetComputeShaderTemplate(template, kernelName);
                if (string.IsNullOrEmpty(content))
                {
                    return AIToolResult.Failed($"Unknown template: {template}");
                }

                string fullPath = Path.Combine(Application.dataPath, path);
                string directory = Path.GetDirectoryName(fullPath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                return AIToolResult.Succeeded($"Created {template} compute shader at Assets/{path}");
            }
            catch (Exception ex)
            {
                return AIToolResult.Failed($"Failed to create compute shader: {ex.Message}");
            }
        }

        #endregion

        #region Templates

        private static string GetShaderTemplate(string template, string shaderName)
        {
            return template.ToLower() switch
            {
                "unlit" => $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _MainTex (""Texture"", 2D) = ""white"" {{}}
        _Color (""Color"", Color) = (1,1,1,1)
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Opaque"" }}
        LOD 100

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata
            {{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            }};

            struct v2f
            {{
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            }};

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {{
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }}

            fixed4 frag (v2f i) : SV_Target
            {{
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }}
            ENDCG
        }}
    }}
}}",

                "surface" => $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _Color (""Color"", Color) = (1,1,1,1)
        _MainTex (""Albedo (RGB)"", 2D) = ""white"" {{}}
        _Glossiness (""Smoothness"", Range(0,1)) = 0.5
        _Metallic (""Metallic"", Range(0,1)) = 0.0
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Opaque"" }}
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {{
            float2 uv_MainTex;
        }};

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {{
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }}
        ENDCG
    }}
    FallBack ""Diffuse""
}}",

                "urp_lit" => $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _BaseMap(""Base Map"", 2D) = ""white"" {{}}
        _BaseColor(""Base Color"", Color) = (1, 1, 1, 1)
        _Smoothness(""Smoothness"", Range(0, 1)) = 0.5
        _Metallic(""Metallic"", Range(0, 1)) = 0
    }}

    SubShader
    {{
        Tags
        {{
            ""RenderType"" = ""Opaque""
            ""RenderPipeline"" = ""UniversalPipeline""
        }}

        Pass
        {{
            Name ""ForwardLit""
            Tags {{ ""LightMode"" = ""UniversalForward"" }}

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {{
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            }};

            struct Varyings
            {{
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            }};

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {{
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }}

            half4 frag(Varyings IN) : SV_Target
            {{
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 color = baseMap * _BaseColor;

                // Simple lighting
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(IN.normalWS, mainLight.direction));
                color.rgb *= mainLight.color * NdotL + half3(0.1, 0.1, 0.1);

                return color;
            }}
            ENDHLSL
        }}
    }}
}}",

                "urp_unlit" => $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _BaseMap(""Base Map"", 2D) = ""white"" {{}}
        _BaseColor(""Base Color"", Color) = (1, 1, 1, 1)
    }}

    SubShader
    {{
        Tags
        {{
            ""RenderType"" = ""Opaque""
            ""RenderPipeline"" = ""UniversalPipeline""
        }}

        Pass
        {{
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""

            struct Attributes
            {{
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            }};

            struct Varyings
            {{
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            }};

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {{
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }}

            half4 frag(Varyings IN) : SV_Target
            {{
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                return color;
            }}
            ENDHLSL
        }}
    }}
}}",

                "image_effect" => $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _MainTex (""Texture"", 2D) = ""white"" {{}}
        _Intensity (""Effect Intensity"", Range(0, 1)) = 1
    }}
    SubShader
    {{
        Cull Off ZWrite Off ZTest Always

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            struct appdata
            {{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            }};

            struct v2f
            {{
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            }};

            sampler2D _MainTex;
            float _Intensity;

            v2f vert (appdata v)
            {{
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }}

            fixed4 frag (v2f i) : SV_Target
            {{
                fixed4 col = tex2D(_MainTex, i.uv);
                // Apply your effect here
                fixed gray = dot(col.rgb, fixed3(0.299, 0.587, 0.114));
                col.rgb = lerp(col.rgb, fixed3(gray, gray, gray), _Intensity);
                return col;
            }}
            ENDCG
        }}
    }}
}}",

                "vertex_fragment" => $@"Shader ""{shaderName}""
{{
    Properties
    {{
        _MainTex (""Texture"", 2D) = ""white"" {{}}
        _Color (""Color"", Color) = (1,1,1,1)
    }}
    SubShader
    {{
        Tags {{ ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }}
        LOD 100

        Pass
        {{
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include ""UnityCG.cginc""

            struct appdata
            {{
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            }};

            struct v2f
            {{
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                float4 vertex : SV_POSITION;
            }};

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {{
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }}

            fixed4 frag (v2f i) : SV_Target
            {{
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Simple lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(i.worldNormal, lightDir));
                col.rgb *= NdotL * 0.5 + 0.5; // Half Lambert

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }}
            ENDCG
        }}
    }}
    FallBack ""Diffuse""
}}",

                _ => null
            };
        }

        private static string GetComputeShaderTemplate(string template, string kernelName)
        {
            return template.ToLower() switch
            {
                "basic" => $@"#pragma kernel {kernelName}

// Result buffer
RWStructuredBuffer<float> Result;

[numthreads(8,8,1)]
void {kernelName} (uint3 id : SV_DispatchThreadID)
{{
    // Your compute logic here
    Result[id.x + id.y * 8] = id.x * 0.1;
}}",

                "texture" => $@"#pragma kernel {kernelName}

// Input/Output textures
Texture2D<float4> Input;
RWTexture2D<float4> Result;

[numthreads(8,8,1)]
void {kernelName} (uint3 id : SV_DispatchThreadID)
{{
    // Read input
    float4 color = Input[id.xy];

    // Process (example: invert colors)
    color.rgb = 1.0 - color.rgb;

    // Write output
    Result[id.xy] = color;
}}",

                "buffer" => $@"#pragma kernel {kernelName}

struct Data
{{
    float3 position;
    float3 velocity;
}};

// Buffers
RWStructuredBuffer<Data> dataBuffer;
float deltaTime;

[numthreads(64,1,1)]
void {kernelName} (uint3 id : SV_DispatchThreadID)
{{
    Data data = dataBuffer[id.x];

    // Update position based on velocity
    data.position += data.velocity * deltaTime;

    dataBuffer[id.x] = data;
}}",

                _ => null
            };
        }

        #endregion
    }
}
