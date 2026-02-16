## Summary

- What changed
- Why this change is needed

## Scope

- [ ] managed (`engine/managed`)
- [ ] native (`engine/native`)
- [ ] tools (`engine/tools`)
- [ ] docs/templates/samples

## Architecture Impact

- Managed/native ABI changed: yes/no
- RenderGraph topology changed: yes/no
- Asset or scene format changed: yes/no

If any answer is yes, describe compatibility impact and migration steps.

## Tests

Commands executed:

```bash
dotnet test dff.sln --nologo
cmake --build engine/native/build --config Debug
ctest --test-dir engine/native/build -C Debug --output-on-failure
```

Additional tests:

- ...

## Checklist

- [ ] Tests pass
- [ ] Docs updated
- [ ] No hidden fallback behavior introduced
- [ ] API/ABI version bumped if required
