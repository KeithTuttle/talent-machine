<script setup lang="ts">
import AppShell from '@/components/layout/AppShell.vue'
import AuthGate from '@/components/layout/AuthGate.vue'

// When Clerk isn't configured we render the app open (dev). When it is, AuthGate
// takes over: it calls Clerk's useAuth() and shows sign-in when signed out.
const authEnabled = !!import.meta.env.VITE_CLERK_PUBLISHABLE_KEY
</script>

<template>
  <AuthGate v-if="authEnabled" />
  <AppShell v-else />
</template>
