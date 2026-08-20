# Feature

This folder is a placeholder for one business feature.

Replace `Feature` with the real feature name, such as `Customers`, `Invoices`, or `Orders`.

```text
Feature/
  Commands/
  Queries/
  Validators/
  Mappings/
  Hooks/
  Automations/
  Querying/
  Models/
    Requests/
    Responses/
  Services/
```

Use `Querying/` for DataScorpio `QueryProfile<TEntity>` classes that describe the filters, sorts, search fields, and aliases allowed by that feature.

For shared code used by more than one feature, prefer a small explicit folder at the business root.
