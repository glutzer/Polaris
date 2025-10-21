#version 330 core
#extension GL_ARB_explicit_attrib_location : enable
#extension GL_ARB_shading_language_420pack : require

in vec2 uv;

uniform int shaderType;
uniform sampler2D tex2d;
uniform vec4 color = vec4(1.0);
uniform vec4 fontColor = vec4(1.0);
uniform float fade;
uniform float renderWidth;
uniform float renderHeight;
uniform float zoom;

// 9 slice stuff.
uniform vec4 dimensions;
uniform vec4 border;
uniform vec2 centerScale;
uniform int bold;

// Star shader.
uniform float time;
uniform vec2 offset;
vec4 star1Color = vec4(0.7, 0.4, 0.7, 0.7);
vec4 star2Color = vec4(0.3, 0.3, 0.8, 0.7);
vec4 star3Color = vec4(0.1, 0.2, 0.9, 0.7);
vec4 topColor = vec4(0.1, 0.1, 0.2, 1.0);
float grid = 100.0;
float size = 0.1;
vec2 speed = vec2(0.0, 3.0);

uniform int starCount;

struct StarLight {
  vec4 PosRange;
  vec4 Color;
};

layout(std140) uniform starLightData { StarLight lights[1000]; };

// 2D Random
float random(in vec2 st) {
  return fract(sin(dot(st.xy, vec2(12.9898, 78.233))) * 43758.5453123);
}

// 2D Noise based on Morgan McGuire @morgan3d
// https://www.shadertoy.com/view/4dS3Wd
float noise(in vec2 st) {
  vec2 i = floor(st);
  vec2 f = fract(st);

  // Four corners in 2D of a tile
  float a = random(i);
  float b = random(i + vec2(1.0, 0.0));
  float c = random(i + vec2(0.0, 1.0));
  float d = random(i + vec2(1.0, 1.0));

  // Smooth Interpolation

  // Cubic Hermine Curve.  Same as SmoothStep()
  vec2 u = f * f * (3.0 - 2.0 * f);
  // u = smoothstep(0.,1.,f);

  // Mix 4 coorners percentages
  return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

#define OCTAVES 4
float fbm(in vec2 st) {
  // Initial values
  float value = 0.0;
  float amplitude = .5;
  float frequency = 0.;
  //
  // Loop of octaves
  for (int i = 0; i < OCTAVES; i++) {
    value += amplitude * noise(st);
    st *= 2.;
    amplitude *= .5;
  }
  return value;
}

vec2 randVector(in vec2 vec, in float seed) {
  return vec2(fract(sin(vec.x * 999.9 + vec.y) * seed),
              fract(sin(vec.y * 999.9 + vec.x) * seed));
}

void drawStars(inout vec4 fragColor, in vec4 color, in vec2 uv, in float grid,
               in float size, in vec2 speed, in float seed) {
  uv += time * speed;

  // Split uv into local grid.
  vec2 local = mod(uv, grid) / grid;

  // Random vector for each grid cell.
  vec2 randv = randVector(floor(uv / grid), seed) - 0.5;
  float len = length(randv);

  // If center + random vector lies inside cell.
  // Draw circle.
  if (len < 0.5) {
    // Draw circle on local grid.
    float radius =
        1.0 - distance(local, vec2(0.5, 0.5) + randv) / (size * (0.5 - len));
    if (radius > 0.0)
      fragColor += color * radius;
  }
}

// const float smoothing = 1.0 / 16.0;
const float pxRange = 12.0;

out vec4 fragColor;

float median(float r, float g, float b) {
  return max(min(r, g), min(max(r, g), b));
}

float screenPxRange() {
  vec2 unitRange = vec2(pxRange) / vec2(textureSize(tex2d, 0));
  vec2 screenTexSize = vec2(1.0) / fwidth(uv);
  return max(0.5 * dot(unitRange, screenTexSize), 1.0);
}

float map(float value, float originalMin, float originalMax, float newMin,
          float newMax) {
  return (value - originalMin) / (originalMax - originalMin) *
             (newMax - newMin) +
         newMin;
}

float processAxis(float coord, vec2 textureBorder, vec2 windowBorder,
                  float scale) {
  // Before.
  if (coord < windowBorder.x)
    return map(coord, 0, windowBorder.x, 0, textureBorder.x);

  // Middle.
  if (coord < 1 - windowBorder.y) {
    float mappedValue = map(coord, windowBorder.x, 1 - windowBorder.y,
                            textureBorder.x, 1 - textureBorder.y);

    float dist = (mappedValue - textureBorder.x) * scale;

    dist = mod(dist, 1 - (textureBorder.x + textureBorder.y));

    return textureBorder.x + dist;
  }

  // After.
  return map(coord, 1 - windowBorder.y, 1, 1 - textureBorder.y, 1);
}

vec4 do9Slice() {
  vec2 newUV =
      vec2(processAxis(uv.x, vec2(border.x, border.z),
                       vec2(dimensions.x, dimensions.z), centerScale.x),
           processAxis(uv.y, vec2(border.y, border.w),
                       vec2(dimensions.y, dimensions.w), centerScale.y));

  return texture(tex2d, newUV);
}

void main() {
  // Background.
  fragColor = topColor;

  vec2 offsetN = offset;
  offsetN.x = -offsetN.x;

  vec2 offsetFromCenter =
      gl_FragCoord.xy - vec2(renderWidth, renderHeight) / 2.0;
  offsetFromCenter *= zoom;

  // "World space" pixel.
  vec2 unOffsetPixel = vec2(renderWidth, renderHeight) / 2.0 + offsetFromCenter;
  vec2 pixel = unOffsetPixel + offsetN;
  pixel.y -= renderHeight;

  // Stars.
  drawStars(fragColor, star1Color, unOffsetPixel + offsetN, grid, size, speed,
            123456.789);
  drawStars(fragColor, star2Color, unOffsetPixel + offsetN / 1.5,
            grid * 2.0 / 3.0, size, speed / 1.2, 345678.912);
  drawStars(fragColor, star3Color, unOffsetPixel + offsetN / 2.0, grid / 2.0,
            size * 3.0 / 4.0, speed / 1.6, 567891.234);

  for (int i = 0; i < starCount; i++) {
    float distanceFrom = length(pixel - lights[i].PosRange.xy);
    if (distanceFrom > lights[i].PosRange.z)
      continue;

    float distanceNoise = fbm(1000.0 + pixel / 50.0 + time / 10.0);
    distanceFrom += distanceNoise * lights[i].PosRange.z * 1.0;

    // 1.0 at center, 0.0 at range.
    float attenuation =
        1.0 - clamp(distanceFrom / lights[i].PosRange.z, 0.0, 1.0);
    float power = fbm(pixel / 50.0 + time / 10.0) * attenuation;

    fragColor += lights[i].Color * power;
  }

  float fadeFactor = clamp(1.0 - fade, 0.0, 1.0);
  fragColor.a *= fadeFactor;
}