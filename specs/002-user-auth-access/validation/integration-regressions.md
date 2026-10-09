# Legacy integration regression disposition

Full run:207 passed,29 failed,0 skipped. Full private TRX remains ignored under .local/feature002-validation/. No failure is called passing and no prior assertions were edited.

| Test | Failure category |
|---|---|
| Crm.IntegrationTests.Persistence.DevelopmentMigrationStartupTests.Development_startup_applies_pending_compiled_migrations_before_authenticated_readiness(state: InitialStorage) | Old-schema fixture uses LocalUserId before new migration |
| Crm.IntegrationTests.Tickets.CreateAndViewTests.Authorized_create_read_and_history_match_atomic_saved_defaults_and_snapshot | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(from: "InProgress", to: "Resolved", allowed: True) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Persistence.DevelopmentMigrationStartupTests.Repeated_startup_does_not_reexecute_applied_migrations_or_change_saved_data | Old startup fixture lacks local-account binding |
| Crm.IntegrationTests.Security.MigrationEnvironmentBoundaryTests.Non_Development_hosts_start_with_valid_auth_but_never_apply_pending_migrations(environment: "Testing") | Old external-auth configuration/DI fixture |
| Crm.IntegrationTests.Security.AuthorizationBoundaryTests.Production_requires_normal_TLS_and_JWT_validation_without_diagnostic_details | Old external-auth configuration/DI fixture |
| Crm.IntegrationTests.Tickets.ReferenceProvisioningTests.Identity_rebind_rolls_back_earlier_staged_category_changes | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Tickets.TicketHistoryTests.Reference_renaming_and_new_reader_identity_do_not_rewrite_any_history_snapshot | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(from: "Resolved", to: "Closed", allowed: True) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.EditAssignmentConcurrencyTests.New_selection_lock_serializes_provisioning_and_keeps_event_label_snapshot(assignment: True) | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(from: "Open", to: "Resolved", allowed: True) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.EditAssignmentConcurrencyTests.New_selection_lock_serializes_provisioning_and_keeps_event_label_snapshot(assignment: False) | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Tickets.EditAssignmentConcurrencyTests.Inactivation_keeps_versions_history_and_retained_references_but_rejects_new_selections | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Close_reopen_keeps_inactive_references_and_blocks_even_noop_details_and_assignment | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(from: "Closed", to: "Open", allowed: True) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.EditAssignmentConcurrencyTests.Actual_changes_are_atomic_readable_and_fresh_noops_preserve_token_time | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Security.MigrationEnvironmentBoundaryTests.Development_design_time_construction_has_no_migration_side_effect_until_explicit_update(state: Fresh) | Old migration inventory (2 rather than4) |
| Crm.IntegrationTests.Persistence.DevelopmentMigrationStartupTests.Fixture_preparation_preserves_the_requested_migration_state(state: Current) | Old migration inventory (2 rather than4) |
| Crm.IntegrationTests.Security.MigrationEnvironmentBoundaryTests.Non_Development_hosts_start_with_valid_auth_but_never_apply_pending_migrations(environment: "Preview") | Old external-auth configuration/DI fixture |
| Crm.IntegrationTests.Security.AuthorizationBoundaryTests.Route_inventory_is_exact_and_every_action_requires_shared_queue_authorization | Old authorization-policy inventory |
| Crm.IntegrationTests.Security.MigrationEnvironmentBoundaryTests.Development_design_time_construction_has_no_migration_side_effect_until_explicit_update(state: InitialStorage) | Old migration inventory (2 rather than4) |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(from: "Resolved", to: "Open", allowed: True) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Every_state_pair_obeys_the_contract_and_produces_only_actual_readable_history(from: "Open", to: "InProgress", allowed: True) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Tickets.TicketStatusTests.Real_JWT_authorization_precedes_mutation_validation(subject: "staff-a", allowed: False, expired: False, issuer: "https://example.test", audience: "crm-tickets-tests", expected: 403) | Old external actor/issuer/role expectation |
| Crm.IntegrationTests.Security.MigrationEnvironmentBoundaryTests.Non_Development_hosts_start_with_valid_auth_but_never_apply_pending_migrations(environment: "Production") | Old external-auth configuration/DI fixture |
| Crm.IntegrationTests.Persistence.DevelopmentMigrationStartupTests.Development_startup_applies_pending_compiled_migrations_before_authenticated_readiness(state: Fresh) | Old migration inventory (2 rather than4) |
| Crm.IntegrationTests.Tickets.TicketHistoryTests.Real_commands_emit_a_complete_account_and_failed_noop_operations_add_zero_entries | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Tickets.ReferenceProvisioningTests.Upsert_noop_omission_inactivation_and_snapshot_retention_follow_contract | Old Agent provisioning/external identity fixture |
| Crm.IntegrationTests.Security.MigrationEnvironmentBoundaryTests.Non_Development_hosts_start_with_valid_auth_but_never_apply_pending_migrations(environment: "Staging") | Old external-auth configuration/DI fixture |

Later Feature002 integration tasks adapt fixtures while preserving business assertions. The foundation independently verifies migration preservation, local role/current-state authorization, cryptography and category-only provisioning. Historical Feature001 artifacts remain unchanged.
