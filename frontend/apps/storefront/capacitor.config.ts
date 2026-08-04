import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.gentlestore.app',
  appName: 'GentleStore',
  webDir: 'dist',
  server: {
    androidScheme: 'https',
  },
};

export default config;
