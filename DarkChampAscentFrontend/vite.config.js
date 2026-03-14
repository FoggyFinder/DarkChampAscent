// vite.config.js
export default {
    define: {
        global: 'globalThis'   // fixes Defly's CommonJS dep
    }
}