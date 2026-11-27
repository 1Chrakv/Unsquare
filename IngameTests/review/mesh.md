## Checklist: deformed mesh

- The building follows the cage: walls, roof and corners sit where the cage's corners and edges are.
- Surfaces are smooth: no torn or doubled faces, holes, spikes or vertices left behind at the old shape.
- Lighting looks right: no black or inside-out faces, no hard seams where normals were bent, shading follows
  the new slope.
- Details stay attached: windows, signs and roof parts follow their surface and don't float or sink in.
- Compare with the undeformed shot where there is one: only the intended part changed.

Fail only for something visible at the given distance; small aliasing or LOD differences are no failure.
